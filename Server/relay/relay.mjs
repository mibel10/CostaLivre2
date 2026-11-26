import { randomBytes } from 'node:crypto';
import { createServer } from 'node:http';
import { WebSocket, WebSocketServer } from 'ws';

const maxRooms = 64;
const maxGuests = 15;
const rooms = new Map();
const tickets = new Map();
const websocket = new WebSocketServer({ noServer: true, maxPayload: 32772 });
const server = createServer((request, response) => {
    response.writeHead(request.url === '/health' ? 200 : 404, { 'content-type': 'text/plain' });
    response.end(request.url === '/health' ? 'Costa Livre 2 relay\n' : 'Not found\n');
});

function reject(socket, status) {
    socket.end('HTTP/1.1 ' + status + '\r\nConnection: close\r\nContent-Length: 0\r\n\r\n');
}

function createRoom(host) {
    let code;
    do {
        code = randomBytes(5).toString('hex').toUpperCase();
    } while (rooms.has(code));
    const room = { host, guests: new Set() };
    rooms.set(code, room);
    host.send('room ' + code);
    host.on('message', () => host.terminate());
    host.on('close', () => {
        rooms.delete(code);
        for (const guest of room.guests) guest.terminate();
    });
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
        const join = /^\/join\/([A-F0-9]{10})$/.exec(path);
        const peer = /^\/peer\/([a-f0-9]{32})$/.exec(path);
        const room = join && rooms.get(join[1]);
        if (room && room.host.readyState === WebSocket.OPEN && room.guests.size < maxGuests)
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
