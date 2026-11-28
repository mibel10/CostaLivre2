# Costa Livre 2

Jogo de mundo aberto em terceira pessoa ambientado no litoral brasileiro, para jogar **solo** ou **multiplayer** (Internet e rede local). Explore **Santa Luz**, **Calado City**, **Antutu City** e **Vice City**, dirija carros e motos, monte sua garagem, dispute territórios de gangue e cumpra contratos pela cidade.

## Sobre o jogo

- **Mundo aberto tropical**: quatro regiões com identidade própria, ciclo completo de dia e noite, clima dinâmico, mar costeiro, praias, parque, interiores exploráveis e tráfego urbano.
- **Concessionária, garagem e oficina**: compre veículos na **Costa Motors**, guarde-os na sua garagem nas vilas ou na Garagem do Cais, chame seu carro pela rua (`G`) e personalize com peças mecânicas, pintura, neon sob o chassi, vidro fumê e nitro (`Shift`).
- **Gangues e territórios**: sete territórios disputados por três facções rivais (*Tubarões do Cais*, *Sindicato Solar* e *Coroas de Vice*), com guerras de bairro em três ondas, defesas de território e renda passiva acumulada no cofre.
- **Economia e atividades**: contratos, assaltos, loja de armas, banco e caixas eletrônicos, compra de imóveis, academia, pesca, corrida de rua, desafio de drift, basquete e fliperama.
- **Multiplayer**: lista de partidas ativas (LAN e Internet) com entrada em um clique, salas públicas ou privadas com senha opcional, migração automática de anfitrião, papéis de criminoso ou policial e chat de texto (`T`).

## Multiplayer

### Entrar em uma partida
1. Abra **Multiplayer** no menu. No primeiro acesso, escolha seu nome de jogador.
2. Na aba **Partidas**, o jogo lista automaticamente salas abertas na rede local (**LAN**) e na **Internet**.
3. Clique em **Entrar** na sala desejada (se houver cadeado, informe a senha).

### Criar uma sala
1. Vá em **Multiplayer → Criar sala**.
2. Defina o **Nome da sala**, **Senha (opcional)**, visibilidade (**Pública** ou **Privada**) e rede (**Internet** ou **LAN**).
3. Em salas pela Internet, caso o anfitrião saia enquanto houver outros jogadores, o servidor transfere a hospedagem automaticamente para outro participante.

### Servidor Relay
O servidor relay utilizado pelas salas pela Internet fica em `Server/relay` (compatível com `render.yaml`). Para executar localmente:

```bash
cd Server/relay
npm ci
npm start
```

Na aba **Multiplayer → Código / IP**, também é possível conectar por código de convite, IPv4 direto (porta padrão `7777`) ou apontar para um servidor relay próprio.

## Controles

| Entrada | Ação |
| --- | --- |
| W A S D / Analógico esquerdo | Mover / Dirigir |
| E | Interagir, entrar ou sair de veículos e serviços |
| Shift esquerdo | Correr a pé / Acionar Nitro no veículo |
| G | Chamar veículo da garagem |
| T | Chat de texto no Multiplayer (`Enter` envia · `Esc` fecha) |
| Espaço | Pular / Freio de mão no veículo |
| Ctrl esquerdo | Agachar |
| C | Rolar |
| V | Trocar ombro da câmera |
| M | Mapa |
| I | Mochila |
| Tab | Quadro de missões |
| 1 / 2 / 3 | Pistola / Arma pesada / Faca |
| Mouse esquerdo / RT | Atirar / Atacar |
| Mouse direito / LT | Mirar |
| R | Recarregar |
| F5 / Esc | Reiniciar / Pausar |

## Requisitos de sistema

### PC (Windows / Linux)

| Componente | Mínimo (720p/1080p · Baixo/Médio · 30–60 FPS) | Recomendado (1080p · Alto/Ultra · 60 FPS) |
| --- | --- | --- |
| Sistema Operacional | Windows 10 21H1+ ou Ubuntu 22.04/24.04 (64 bits) | Windows 10/11 ou Ubuntu 22.04/24.04 (64 bits) |
| Processador | Intel Core i3-8100 / i5-6400 ou AMD Ryzen 3 3200G | Intel Core i5-10400F ou AMD Ryzen 5 3600 / 5600 |
| Placa de vídeo | GTX 1050 / RX 560 (2–4 GB) ou vídeo integrado moderno | GeForce GTX 1660 / RTX 2060 ou Radeon RX 6600 |
| Memória RAM | 6 GB (8 GB recomendado) | 16 GB |
| Armazenamento | 2 GB livres | 2 GB livres em SSD |

### Mobile (Android / iOS)

| Plataforma | Mínimo (Baixo · 30 FPS) | Recomendado (Médio · 60 FPS) |
| --- | --- | --- |
| Android | Android 7.1+ (API 25, ARM64), Snapdragon 680 / Helio G85 ou equivalente, 4–6 GB RAM | Snapdragon 778G / 870 / 8 Gen 1+, 8 GB RAM |
| iPhone | iPhone XR / iPhone 11 (iOS 15+, Metal) | iPhone 13 / 15 Pro |

## Compilar o jogo

1. Clone [mibel10/CostaLivre2](https://github.com/mibel10/CostaLivre2).
2. Instale o **Unity 6000.3.29f1** pelo Unity Hub com o módulo da plataforma desejada (para Android, inclua SDK, NDK e OpenJDK do Hub).
3. Abra o projeto no Unity Hub, carregue `Assets/Scenes/HarborRun.unity` e pressione **Play** para testar no Editor.

### Windows e Linux (linha de comando)

**Linux (Bash)**
```bash
unity="$HOME/Unity/Hub/Editor/6000.3.29f1/Editor/Unity"
mkdir -p Builds/Linux
"$unity" -batchmode -nographics -quit -projectPath "$PWD" \
  -buildTarget Linux64 -buildLinux64Player "$PWD/Builds/Linux/CostaLivre2.x86_64" \
  -logFile "$PWD/Builds/Linux/build.log"
```

**Windows (PowerShell)**
```powershell
$unity = "$env:ProgramFiles\Unity\Hub\Editor\6000.3.29f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force Builds/Windows | Out-Null
& $unity -batchmode -nographics -quit -projectPath "$PWD" `
  -buildTarget Win64 -buildWindows64Player "$PWD/Builds/Windows/CostaLivre2.exe" `
  -logFile "$PWD/Builds/Windows/build.log"
```

### Android e Xbox

- **Android**: selecione a plataforma Android em **Build Profiles** e execute **Costa Livre → Build → Android APK** (saída em `Builds/Android/CostaLivre2.apk`). Via CLI, use `-buildTarget Android -executeMethod CostaLivre.Editor.CostaPlatformBuild.Android`.
- **Xbox (Dev Mode)**: no Windows com o módulo UWP da Unity e ferramentas UWP/C++ do Visual Studio, execute `build_xbox.bat` ou `build.ps1 -Target xbox`.

## Créditos e assets

- Texturas de asfalto e concreto 2K baseadas em **Asphalt 010** e **Concrete 034** ([ambientCG](https://ambientcg.com/), CC0).
- Vozes em português sintetizadas com **Piper** (modelo `pt_BR/faber/medium`).
