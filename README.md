# Costa Livre 2

Jogo de mundo aberto em terceira pessoa com estética **tropical brasileira inspirada em GTA VI e Vice City**, para jogar **solo ou multiplayer** (LAN e Internet). Explore **Santa Luz**, **Calado City**, **Antutu City** e **Vice City**, conquiste territórios em guerras de bairro, compre e tune carros e motos com neon e nitro, e jogue com amigos entrando direto pela lista de partidas ativas.

## Principais novidades e recursos

- **Direção artística tropical brasileira**: fachadas coloridas por bairro, mar azul-turquesa com espuma costeira em todos os presets, céu vibrante com pôr do sol dourado/coral, palmeiras e ipês floridos, letreiros comerciais, murais de grafite e detalhes Art Déco com neon em Vice City.
- **Menu principal e personagem renovados**: menu com identidade tropical e palco 3D em terraço costeiro; protagonista com cabelo estilizado em camadas, sobrancelhas, streetwear, corrente dourada, relógio, cinto, tênis e acessórios (boné tropical, chapéu *bucket*, óculos aviador e bolsa transversal).
- **Tela de morte estilo GTA V**: sequência cinematográfica em câmera lenta (`MORTO` / `WASTED`), som grave procedural, vinheta, queda de câmera e transição suave até o **Hospital Costeiro** (compatível com Solo e Multiplayer).
- **Concessionária Costa Motors & Garagem Própria**: catálogo com **15 veículos compráveis** e preview 3D giratório; veículos salvos permanentemente com tunagem na garagem das **Vilas compradas** ou **Garagem do Cais**, com entrega rápida na rua pela tecla **`G`**.
- **Oficina & Tunagem Avançada**: 21 categorias de personalização mecânica e visual, incluindo **Neon Underglow** (Rosa Vice, Ciano Tropical e Ouro Solar), **Vidro Fumê** e **Nitro Turbo** (acionado com `Shift` enquanto dirige).
- **Sistema de Gangues, Territórios e Renda Passiva**: **7 territórios coloridos** no mapa e minimapa disputados por **3 gangues rivais** (*Tubarões do Cais*, *Sindicato Solar* e *Coroas de Vice*); guerras de bairro em **3 ondas** estilo GTA San Andreas, contra-ataques rivais e **Cofre de Renda Passiva** coletável no Banco/ATM ou Garagem.
- **Multiplayer com Lista de Partidas, Migração de Anfitrião, Senha e Chat**:
  - **Navegador de Partidas Ativas** com descoberta automática na rede local (**LAN/Wi-Fi**) e salas públicas pela **Internet** (entrada em 1 clique).
  - **Salas Públicas ou Privadas** com **senha opcional (`🔒`)**.
  - **Migração automática de anfitrião**: se quem criou a sala sair, outro jogador assume automaticamente sem derrubar a partida.
  - **Chat de texto** dentro da partida (tecla **`T`** no PC ou botão **Chat** no celular).
- **Otimização agressiva de desempenho (FPS)**: resolução adaptativa ligada por padrão, eliminação de prepass pesado de SSAO, sombras e espelhos recalibrados, atmosfera fullscreen restrita ao Alto/Ultra e animação de NPCs reduzida por distância (`CullUpdateTransforms`).

---

## Compilar o jogo

1. Clone [mibel10/CostaLivre2](https://github.com/mibel10/CostaLivre2) com uma conta que tenha acesso ao repositório.
2. Instale o **Unity 6000.3.29f1** pelo Unity Hub e adicione o módulo da plataforma de destino. Para Android, instale também o SDK, o NDK e o OpenJDK oferecidos pelo Hub. [Requisitos do Unity 6.3](https://docs.unity.com/en-us/engine/6000.3/manual/get-started/install-and-upgrade/getting-started-installing-unity/system-requirements)
3. Adicione a pasta do projeto ao Unity Hub e aguarde a importação dos pacotes. Para testar no Editor, abra `Assets/Scenes/HarborRun.unity` e pressione **Play**.

### Windows e Linux pelo Editor

Em **File → Build Profiles**, selecione **Windows** ou **Linux**, escolha a arquitetura x86-64 e clique em **Switch Platform** se necessário. Confirme que `Assets/Scenes/HarborRun.unity` está habilitada na lista de cenas. Clique em **Build** e salve em uma pasta como `Builds/Windows` ou `Builds/Linux`. Ao mover ou empacotar a build, mantenha o executável junto à pasta de dados e às bibliotecas geradas. [Build Profiles do Unity 6.3](https://docs.unity.com/en-us/engine/6000.3/manual/building-and-publishing/build-settings/build-profiles)

### Windows e Linux por linha de comando

Execute na raiz do projeto com o Editor desse projeto fechado. Ajuste o caminho do Unity se o Hub estiver em outro diretório. [Opções de linha de comando do Unity 6.3](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/editor)

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

- **Android** — ative a plataforma Android em **Build Profiles** e use **Costa Livre → Build → Android APK**. O APK sai em `Builds/Android/CostaLivre2.apk`. Para automatizar, use `-buildTarget Android -executeMethod CostaLivre.Editor.CostaPlatformBuild.Android` com as opções de projeto e log acima. Defina `COSTA_BUILD_OUTPUT` para mudar o destino. [Builds Android no Unity](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/building-and-delivering/build-process)
- **Xbox em Dev Mode** — no Windows com o módulo Universal Windows Platform da Unity e as ferramentas UWP/C++ do Visual Studio, execute `build_xbox.bat` ou `build.ps1 -Target xbox`. O script gera o projeto UWP x64 em `Builds/UWP` e empacota o `.appx`. [Plataformas fechadas no Unity](https://docs.unity.com/en-us/engine/6000.3/manual/building-and-publishing/build-settings/build-profiles-reference)

---

## Requisitos de PC estimados (pós-otimização)

| Componente | Mínimo (720p/1080p · Baixo/Médio · 30–60 FPS) | Recomendado (1080p · Alto/Ultra · 60 FPS) |
| --- | --- | --- |
| Sistema Operacional | Windows 10 21H1+ ou Ubuntu 22.04/24.04 (64 bits) | Windows 10/11 ou Ubuntu 22.04/24.04 (64 bits) |
| Processador | Intel Core i3-8100 / i5-6400 ou AMD Ryzen 3 3200G | Intel Core i5-10400F ou AMD Ryzen 5 3600 / 5600 |
| Placa de vídeo | GTX 1050 / RX 560 (2–4 GB) ou vídeo integrado moderno (Vega 8 / Iris Xe) | GeForce GTX 1660 / RTX 2060 ou Radeon RX 6600 |
| Memória RAM | 6 GB (8 GB recomendado) | 16 GB |
| Espaço em disco | 2 GB livres | 2 GB livres em SSD |

### Perfil gráfico atual por preset

- **Super baixo / Baixo**: escala 50–70%, sem atmosfera fullscreen, sem espelhos pesados, sombras desligadas ou 512 px até 26 m, distância de visão 85–135 m.
- **Médio (padrão no PC)**: escala 85% com resolução adaptativa ativa, FXAA, sombras 1024 px com 2 cascatas até 48 m, distância de visão 200 m e água costeira turquesa ativa.
- **Alto**: escala 95%, SMAA Baixo, sombras 1024 px com 2 cascatas até 72 m, atmosfera costeira ativa e distância 280 m.
- **Ultra**: escala 100%, SMAA Médio, sombras 2048 px com 4 cascatas até 130 m, sondas de reflexo em tempo real, microtexturas e distância 420 m.

---

## Requisitos estimados para Android e iPhone

| Plataforma | Mínimo (Baixo · 30 FPS) | Recomendado (Médio · 60 FPS) |
| --- | --- | --- |
| Android | Android 7.1+ (API 25, ARM64), Snapdragon 680 / Helio G85 ou equivalente, 4–6 GB RAM | Snapdragon 778G / 870 / 8 Gen 1+, 8 GB RAM |
| iPhone | iPhone XR / iPhone 11 (iOS 15+, Metal) | iPhone 13 / 15 Pro |

A resolução adaptativa vem ativada por padrão para manter a fluidez mesmo em sessões longas e dispositivos móveis com aquecimento. [Configuração do projeto](ProjectSettings/ProjectSettings.asset) · [Requisitos do Unity 6.3](https://docs.unity.com/en-us/engine/6000.3/manual/get-started/install-and-upgrade/getting-started-installing-unity/system-requirements)

---

## Multiplayer e controles

### Lista de Partidas Ativas (Internet e LAN)

1. Abra **Multiplayer → Partidas**. O jogo busca automaticamente:
   - Salas abertas na mesma rede **Wi-Fi / LAN** (via UDP broadcast na porta `47777`, sem precisar digitar IP).
   - Salas públicas abertas pela **Internet** no servidor relay padrão (`https://costalivre2-mibel10-relay.onrender.com`).
2. Clique em **Entrar** em qualquer sala da lista. Se a sala tiver cadeado (**`🔒`**), digite a senha para entrar.

### Criar uma Sala

1. Vá em **Multiplayer → Criar sala**.
2. Escolha **Seu Nome**, **Nome da Sala**, **Senha (opcional)**, visibilidade (**Pública** para aparecer na lista ou **Privada** para entrar só por código) e rede (**Internet** ou **Rede Local LAN**).
3. Se o anfitrião sair enquanto ainda houver outros jogadores na sala pela Internet, o servidor mantém o código reservado e **migra automaticamente o anfitrião** para o próximo jogador sem derrubar a partida.

### Código / IP e Servidor Relay próprio

Na aba **Multiplayer → Código / IP**, ainda é possível entrar por código de convite (Relay ou Noray `CL2N-`), conectar por IPv4/Porta (padrão `7777`) ou apontar para um servidor relay próprio.

O servidor relay está em `Server/relay` (compatível com o Blueprint `render.yaml` no plano gratuito do Render). Para rodar localmente:

```bash
cd Server/relay
npm ci
npm start
```

O servidor expõe `GET /health`, `GET /rooms` (lista JSON de salas públicas) e WebSocket em `/host`, `/join/:code`, `/claim/:code` (migração de anfitrião) e `/peer/:ticket` na porta `8080`.

### Controles

| Entrada | Ação |
| --- | --- |
| W A S D / Analógico esquerdo | Mover / Dirigir |
| E | Interagir, entrar/sair de veículo, Concessionária, Garagem, Oficina e Banco |
| Shift esquerdo | Correr a pé · **Acionar Nitro Turbo** (no veículo tunado) |
| G | **Chamar veículo da garagem própria** para a rua |
| T | **Abrir chat de texto** no Multiplayer (`Enter` envia · `Esc` fecha) |
| Espaço | Pular / Freio de mão no veículo |
| Ctrl esquerdo | Agachar |
| C | Rolar / Esquiva |
| V | Trocar ombro da câmera |
| M | Mapa (com territórios de gangue e rota GPS) |
| I | Mochila |
| Tab | Quadro de missões (Solo) |
| 1 / 2 / 3 | Pistola / Arma pesada / Faca |
| Botão esquerdo do mouse ou RT | Atirar / Atacar |
| Botão direito do mouse ou LT | Mirar |
| R | Recarregar |
| F5 / Esc | Reiniciar / Pausar |

---

## Polimento e autoria na Unity

Em **Opções → Tela e som** ficam o HUD completo, contextual e mínimo, o fundo da cidade e a resolução dos espelhos. O modo automático usa 128, 256 ou 512 px conforme o preset. No Super baixo, ele desativa as capturas e usa o fundo leve.

Edite `CampaignDefinition` no Inspector para definir destinos, condições, objetivos opcionais e diálogos. Mantenha os identificadores usados em saves e use **Validar campanha** no menu de contexto do asset. As cutscenes em `Resources/Harbor/Cinematics` são editáveis no Inspector. Para refazer os ciclos de locomoção após trocar o rig, use **Costa Livre → Animação → Reconstruir Blend Tree direcional**.

As falas da campanha são sintetizadas em português com Piper e o modelo Faber. [Ficha da versão usada](https://huggingface.co/rhasspy/piper-voices/blob/613b6e5ad36ddca4e70e171957291483dc244d9f/pt/pt_BR/faber/medium/MODEL_CARD)

## Texturas

As texturas de asfalto e concreto 2K usam os assets Asphalt 010 e Concrete 034 do ambientCG, sob CC0.
