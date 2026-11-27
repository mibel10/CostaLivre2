# Transformação do Mundo Aberto — Costa Livre 2

Registro de progresso das etapas implementadas para continuidade entre sessões.

## Concluído nesta sessão

### Correções Imediatas (UX, HUD, Controles e Renderização)
- **Direção no controle com Triggers** ([HarborRun.cs](Assets/HarborPrototype/HarborRun.cs)): `RT` acelera e `LT` freia/dá ré quando um gamepad está ativo, mantendo o analógico esquerdo para esterçamento e `W`/`S` no teclado.
- **Minimapa corrigido** ([CostaMap.cs](Assets/HarborPrototype/CostaMap.cs)): corrigida a rotação em torno do pivô dentro de `GUI.BeginGroup` com `GUI.matrix` escalada (que fazia o minimapa sair do centro ao girar a câmera), além de adicionar pins dos novos estabelecimentos e círculo de área de busca policial.
- **HUD melhorado** ([HarborHud.cs](Assets/HarborPrototype/HarborHud.cs), [HarborRun.cs](Assets/HarborPrototype/HarborRun.cs)): barra de vida sempre visível no modo livre e contratos, e velocímetro ampliado com leitura de `km/h` em destaque e barra proporcional.
- **Z-fighting reduzido** ([CostaCity.cs](Assets/HarborPrototype/CostaCity.cs), [CostaVenueLife.cs](Assets/HarborPrototype/CostaVenueLife.cs), [CostaInteriors.cs](Assets/HarborPrototype/CostaInteriors.cs)): ajustadas sobreposições coplanares em cantos de fachadas, pisos de entrada, tapetes, janelas e parapeitos de terraço.

### Etapa 1 — Fundação: Interação Universal, Estabelecimentos e Progressão
- **[CostaWorldInteractive.cs](Assets/HarborPrototype/CostaWorldInteractive.cs)**:
  - Sistema universal de nós interativos com suporte a horário de funcionamento, raio de interação, prompts bilíngues e desconto por reputação da cidade.
  - Novos estabelecimentos funcionais espalhados por **Santa Luz**, **Calado City**, **Antutu City** e **Vice City**: *Empório Santa Luz*, *Restaurante Calado*, *Academia Ferro & Mar*, *Arcade Neon*, *Boate Vice Nights*, *Tunagem Antutu*, *Quadra de Basquete*, *Pesca no Cais*, *Desafio de Drift*, *Corrida de Rua*, bancos de praça para descanso e **12 relíquias secretas** colecionáveis em telhados, becos e terraços.
  - Reputação por cidade (`CostaCityRep_0..3`) e salvamento integrado ao `CostaLedger`.

### Etapa 2 — Sistema Policial Tático (5 Estrelas, Combate Armado e Fuga)
- **[CostaPoliceSystem.cs](Assets/HarborPrototype/CostaPoliceSystem.cs)**:
  - Policiais armados (`2★+`) com distância de combate, movimentação lateral, verificação de linha de visão (`Physics.Linecast`), munição finita, recarga, flash de disparo e som espacial.
  - Escalada de **1★ a 5★**: abordagem a pé (`1★`), tiros e reforços (`2★`), viaturas policiais com giroflex em perseguição (`3★`), unidades táticas e bloqueios de rua (`4★`), e helicóptero policial com holofote (`5★`).
  - Sistema de última posição conhecida (`LastKnownPosition`), estados **AVISTADO** vs **OCULTO / DESPISTANDO** (quebrando linha de visão em interiores, becos ou telhados) e respawn no Hospital Costeiro quando a vida chega a 0 no modo livre.

### Etapa 3 — População Viva, Eventos Dinâmicos e Minigames Jogáveis
- **[CostaLivingWorld.cs](Assets/HarborPrototype/CostaLivingWorld.cs)**:
  - Pool de 24 cidadãos ativados por proximidade (<= 68 m), caminhando entre pontos da cidade e fugindo ao ouvir tiros ou durante perseguições policiais.
  - Eventos dinâmicos orgânicos (*Feira Relâmpago*, *Encontro Automotivo*, *Caixa de Carga Perdida*, *Comerciante Misterioso*, *Festa de Rua Noturna*) com marcador no minimapa e recompensa.
- **[CostaMinigames.cs](Assets/HarborPrototype/CostaMinigames.cs)**:
  - 5 minigames jogáveis com recordes salvos em `PlayerPrefs` e recompensas: **Basquete de Rua**, **Pesca no Cais**, **Arcade Neon**, **Academia Ferro & Mar**, além de **Desafio de Drift** e **Corrida de Rua** com checkpoints no mundo.

## Validação Realizada
- Compilação C# validada via Roslyn (`csc.dll` do Unity `6000.3.29f1`) com 0 erros em todos os scripts de `Assembly-CSharp` (sem gerar build de player, conforme solicitado).

## Próximos Passos Sugeridos (Sessões Futuras)
- Expandir ainda mais o número de interiores multi-andares acessíveis (shoppings, cinemas, delegacia completa, estações de transporte) e missões secundárias narrativas exclusivas por cidade.
- Testar in-game no Editor/Xbox para balancear recompensas, dano policial e tempos de fuga.
