import { randomBytes } from 'node:crypto';
import { createServer } from 'node:http';
import { WebSocket, WebSocketServer } from 'ws';

const maxRooms = 64;
const maxGuests = 15;
const rooms = new Map();
const tickets = new Map();
const websocket = new WebSocketServer({ noServer: true, maxPayload: 32772 });

function cleanText(value, limit, fallback) {
    if (typeof value !== 'string') return fallback;
    const trimmed = value.replace(/[\r\n\t\0]/g, ' ').trim().slice(0, limit);
    return trimmed.length > 0 ? trimmed : fallback;
}

function publicRoomsPayload() {
    const list = [];
    for (const [code, room] of rooms) {
        if (!room.isPublic || room.migrating || room.host.readyState !== WebSocket.OPEN || room.guests.size >= maxGuests)
            continue;
        list.push({
            code,
            name: room.name,
            host: room.hostName,
            players: Math.max(1, room.players || (1 + room.guests.size)),
            maxPlayers: room.maxPlayers || (maxGuests + 1),
            locked: room.hasPassword === true
        });
    }
    return JSON.stringify({ rooms: list });
}

const server = createServer((request, response) => {
    if (request.method === 'GET' && request.url === '/health') {
        response.writeHead(200, { 'content-type': 'text/plain' });
        response.end('Costa Livre 2 relay\n');
        return;
    }
    if (request.method === 'GET' && request.url === '/rooms') {
        response.writeHead(200, {
            'content-type': 'application/json; charset=utf-8',
            'cache-control': 'no-store',
            'access-control-allow-origin': '*'
        });
        response.end(publicRoomsPayload());
        return;
    }
    response.writeHead(404, { 'content-type': 'text/plain' });
    response.end('Not found\n');
});

function reject(socket, status) {
    socket.end('HTTP/1.1 ' + status + '\r\nConnection: close\r\nContent-Length: 0\r\n\r\n');
}

function attachHostHandlers(code, room, host) {
    room.host = host;
    room.migrating = false;
    if (room.migrateTimer) {
        clearTimeout(room.migrateTimer);
        room.migrateTimer = null;
    }
    host.send('room ' + code);
    host.on('message', (data, binary) => {
        if (binary || data.length > 512) {
            host.terminate();
            return;
        }
        const text = data.toString('utf8');
        if (!text.startsWith('meta ')) {
            host.terminate();
            return;
        }
        try {
            const meta = JSON.parse(text.slice(5));
            room.name = cleanText(meta.name, 32, 'Santa Luz');
            room.hostName = cleanText(meta.host, 20, 'Jogador');
            room.isPublic = meta.public === true;
            room.hasPassword = meta.locked === true;
            const reported = Number(meta.players);
            if (Number.isInteger(reported) && reported >= 1 && reported <= maxGuests + 1)
                room.players = reported;
            const cap = Number(meta.maxPlayers);
            if (Number.isInteger(cap) && cap >= 2 && cap <= maxGuests + 1)
                room.maxPlayers = cap;
        } catch {
            host.terminate();
        }
    });
    host.on('close', () => {
        if (room.host !== host) return;
        const hadGuests = room.guests.size > 0;
        for (const guest of room.guests) guest.terminate();
        room.guests.clear();
        if (hadGuests) {
            room.migrating = true;
            room.migrateTimer = setTimeout(() => {
                if (room.migrating) rooms.delete(code);
            }, 12000);
        } else {
            rooms.delete(code);
        }
    });
}

function createRoom(host) {
    let code;
    do {
        code = randomBytes(5).toString('hex').toUpperCase();
    } while (rooms.has(code));
    const room = {
        host,
        guests: new Set(),
        name: 'Santa Luz',
        hostName: 'Jogador',
        isPublic: false,
        hasPassword: false,
        players: 1,
        maxPlayers: 12,
        migrating: false,
        migrateTimer: null
    };
    rooms.set(code, room);
    attachHostHandlers(code, room, host);
}

function claimRoom(code, newHost) {
    const room = rooms.get(code);
    if (!room || !room.migrating) {
        newHost.terminate();
        return;
    }
    attachHostHandlers(code, room, newHost);
}

function addGuest(room, guest) {
    const ticket = randomBytes(16).toString('hex');
    room.guests.add(guest);
    const waiting = setTimeout(() => guest.terminate(), 8000);
    tickets.set(ticket, { guest, waiting });
    guest.on('message', rejectEarlyData);
    guest.on('close', () => {
        tickets.delete(ticket);
        room.guests.delete(guest);
        clearTimeout(waiting);
    });
    room.host.send('peer ' + ticket);
}

function rejectEarlyData() {
    this.terminate();
}

function pair(ticket, hostPeer) {
    const { guest, waiting } = tickets.get(ticket);
    tickets.delete(ticket);
    clearTimeout(waiting);
    guest.off('message', rejectEarlyData);
    const forward = (source, destination) => {
        source.on('message', (data, binary) => {
            if (!binary || destination.readyState !== WebSocket.OPEN || destination.bufferedAmount > 256 * 1024) {
                source.terminate();
                return;
            }
            destination.send(data, { binary: true });
        });
        source.on('close', () => destination.terminate());
    };
    forward(guest, hostPeer);
    forward(hostPeer, guest);
}

server.on('upgrade', (request, socket, head) => {
    socket.on('error', () => socket.destroy());
    const path = request.url;
    let handle;
    if (path === '/host' && rooms.size < maxRooms) {
        handle = createRoom;
    } else {
        const claim = /^\/claim\/([A-F0-9]{10})$/.exec(path);
        const join = /^\/join\/([A-F0-9]{10})$/.exec(path);
        const peer = /^\/peer\/([a-f0-9]{32})$/.exec(path);
        const claimTarget = claim && rooms.get(claim[1]);
        const room = join && rooms.get(join[1]);
        if (claimTarget && claimTarget.migrating)
            handle = newHost => claimRoom(claim[1], newHost);
        else if (room && !room.migrating && room.host.readyState === WebSocket.OPEN && room.guests.size < maxGuests)
            handle = guest => addGuest(room, guest);
        else if (peer && tickets.has(peer[1]))
            handle = hostPeer => pair(peer[1], hostPeer);
    }
    if (!handle) {
        reject(socket, '404 Not Found');
        return;
    }

    websocket.handleUpgrade(request, socket, head, connection => {
        connection.on('error', () => connection.terminate());
        connection.alive = true;
        connection.on('pong', () => { connection.alive = true; });
        handle(connection);
    });
});
const heartbeat = setInterval(() => {
    for (const connection of websocket.clients) {
        if (!connection.alive) {
            connection.terminate();
            continue;
        }
        connection.alive = false;
        connection.ping();
    }
}, 25000);
heartbeat.unref();
server.listen(Number(process.env.PORT || 8080), process.env.BIND || '0.0.0.0');
