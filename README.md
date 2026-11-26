# Costa Livre 2

Jogo de mundo aberto em terceira pessoa para jogar solo ou em rede local. Explore Santa Luz, Calado City, Antutu City e Vice City, dirija carros e motos e escolha missões no quadro da campanha solo.

## Compilar o jogo

1. Clone [mibel10/CostaLivre2](https://github.com/mibel10/CostaLivre2) com uma conta que tenha acesso ao repositório.
2. Instale o **Unity 6000.3.29f1** pelo Unity Hub e adicione o módulo da plataforma de destino. Para Android, instale também o SDK, o NDK e o OpenJDK oferecidos pelo Hub. [Requisitos do Unity 6.3](https://docs.unity.com/en-us/engine/6000.3/manual/get-started/install-and-upgrade/getting-started-installing-unity/system-requirements)
3. Adicione a pasta do projeto ao Unity Hub e aguarde a importação dos pacotes. Para conferir o jogo no Editor, abra `Assets/Scenes/HarborRun.unity` e pressione **Play**.

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
- **Xbox** — no Windows com acesso aprovado ao módulo Xbox do Unity e ao Microsoft GDK, ative o alvo correspondente e use **Costa Livre → Build → Xbox Series X|S** ou **Xbox One**. As saídas padrão ficam em `Builds/XboxSeries` e `Builds/XboxOne`. Para detalhes do ambiente e da implantação, consulte a documentação inclusa no módulo Xbox. [Plataformas fechadas no Unity](https://docs.unity.com/en-us/engine/6000.3/manual/building-and-publishing/build-settings/build-profiles-reference)

## Requisitos de PC estimados

| Componente | Mínimo estimado para 1080p, Médio, 30 FPS | Recomendado estimado para 1080p, Ultra, 60 FPS |
| --- | --- | --- |
| Processador | Core i5-8400 ou Ryzen 5 2600 | Core i5-12400F ou Ryzen 5 5600 |
| Placa de vídeo dedicada | GeForce GTX 1650 4 GB ou Radeon RX 5500 XT 4 GB | GeForce RTX 3060 Ti 8 GB ou Radeon RX 6700 XT 12 GB |
| RAM | 8 GB | 16 GB |
| Espaço livre para o jogo | 2 GB | 2 GB em SSD |

Estas configurações são estimativas para o jogo compilado. Os dois alvos de FPS ainda precisam ser medidos no hardware listado. A build Linux de referência ocupa cerca de 156 MiB, e a tabela reserva espaço para logs e atualizações. Para PC x86-64, o Unity Player lista Windows 10 21H1 ou posterior e Ubuntu 22.04 ou 24.04. O Editor e seus módulos têm requisitos próprios. [Plataformas e requisitos do Unity 6.3](https://docs.unity.com/en-us/engine/6000.3/manual/get-started/install-and-upgrade/getting-started-installing-unity/system-requirements)

A 1920 × 1080 de saída, o Médio renderiza a 1632 × 918, usa MSAA 2×, sombras de 2048 com duas cascatas até 65 m e câmera até 245 m. O Ultra renderiza a 1920 × 1080, usa MSAA 4×, sombras de 4096 com quatro cascatas até 175 m e câmera até 540 m. O Ultra processa cerca de 38% mais pixels antes do antialiasing, mantém mais geometria visível e acrescenta oclusão ambiente mais detalhada, gradação HDR e microtexturas nas superfícies. A geração procedural da cidade e a navegação aumentam o custo de carregamento, enquanto o tráfego de jogadores e os NPCs aumentam o trabalho da CPU durante a partida. [Configuração do Médio](Assets/Settings/Medium_RPAsset.asset) · [Configuração do Ultra](Assets/Settings/Ultra_RPAsset.asset)

Em uma medição curta no centro de Santa Luz, com Core i5-4210U, Intel HD Graphics 4400 e Linux/OpenGL, a câmera renderizou para um alvo de 1080p sem MSAA. O Médio ficou em cerca de 12 FPS e o Ultra em 6 FPS. A tentativa de repetir com MSAA no alvo de teste apresentou avisos de renderização. Essa referência mede apenas o desempenho da máquina testada e foi obtida antes da mudança do Ultra para resolução nativa e do ajuste de iluminação.

Para validar os alvos, meça no executável a 1920 × 1080 com VSync desligado e limite de 60 FPS. Depois do carregamento, faça o mesmo percurso pela região central e repita durante uma missão com NPCs. Compare o tempo de quadro no percentil 95 com 33,3 ms no Médio e 16,7 ms no Ultra. Registre também o pico de RAM e VRAM.

## Requisitos estimados para Android e iPhone

| Plataforma | Mínimo estimado para Baixo, 30 FPS | Recomendado estimado para Médio, 60 FPS |
| --- | --- | --- |
| Android | Snapdragon 778G ou equivalente, 6 GB de RAM | Snapdragon 8 Gen 2 ou equivalente, 8 GB de RAM |
| iPhone | iPhone 11 (A13 Bionic) | iPhone 15 Pro (A17 Pro) |

O projeto configura Android 7.1 (API 25) ou posterior, ARM64 e Vulkan ou OpenGL ES 3, e iOS 15 ou posterior com Metal. Esses limites são de compatibilidade da plataforma. A tabela indica aparelhos de referência para avaliação de desempenho. A exportação para iPhone ainda precisa ser validada em um aparelho. [Configuração do projeto](ProjectSettings/ProjectSettings.asset) · [Requisitos do Unity 6.3](https://docs.unity.com/en-us/engine/6000.3/manual/get-started/install-and-upgrade/getting-started-installing-unity/system-requirements)

O preset Baixo renderiza a 70% da largura e da altura da resolução de saída, com sombras até 30 m. O Médio renderiza a 85%, com sombras até 65 m. A resolução da tela e o aquecimento durante sessões longas podem afetar o FPS. As metas de desempenho ainda precisam ser medidas nos aparelhos indicados. [Configuração do Baixo](Assets/Settings/Mobile_RPAsset.asset) · [Configuração do Médio](Assets/Settings/Medium_RPAsset.asset)

Referências dos modelos e chips. [Snapdragon 778G](https://www.qualcomm.com/smartphones/products/7-series/snapdragon-778g-5g-mobile-platform) · [Snapdragon 8 Gen 2](https://www.qualcomm.com/smartphones/products/8-series/snapdragon-8-gen-2-mobile-platform) · [iPhone 11](https://support.apple.com/en-us/111865) · [iPhone 15 Pro](https://support.apple.com/en-us/111829)

## Multiplayer e controles

### Jogar pela internet

No menu **Multiplayer → Internet**, o anfitrião preenche **Endereço do servidor** com o endereço HTTPS do relay e clica em **Criar sala**. Depois, usa **Copiar convite** e manda o convite aos amigos. Os convidados colam o texto em **Código ou convite da sala** e clicam em **Entrar na sala**. Todos usam a mesma versão da build. A sessão dura enquanto o anfitrião estiver conectado.

O relay está em `Server/relay`. Para rodar uma instância com Node.js, execute:

```bash
cd Server/relay
npm ci
npm start
```

O relay escuta na porta `8080` da máquina local por padrão. Para disponibilizá-lo pela internet, publique-o atrás de HTTPS com suporte a WebSocket. Para um teste temporário, execute `cloudflared tunnel --url http://127.0.0.1:8080` e use a URL HTTPS gerada no jogo. O endereço temporário depende do processo do túnel. [Documentação do Quick Tunnel](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/)

### Jogar na rede local

Em **Multiplayer → LAN**, um jogador seleciona **Hospedar LAN**. Os demais usam **Entrar por IP** com o IPv4 do anfitrião e a mesma porta. A porta padrão é `7777`.

### Controles

| Entrada | Ação |
| --- | --- |
| E | Interagir e entrar ou sair do veículo |
| Shift esquerdo | Correr |
| I | Mochila |
| M | Mapa |
| Ctrl esquerdo | Agachar |
| 1 | Pistola |
| 2 | Arma pesada |
| 3 | Faca |
| Botão esquerdo do mouse ou RT | Atacar com a arma equipada |
| LB / RB ou botão **Trocar** na tela | Alternar entre as armas disponíveis |

Todo jogador começa com uma faca, que fica no terceiro slot. Equipá-la aumenta a velocidade de corrida. Armas pesadas reduzem a velocidade de caminhada.

## Texturas

As texturas de asfalto e concreto 2K usam os assets Asphalt 010 e Concrete 034 do ambientCG, sob CC0.
