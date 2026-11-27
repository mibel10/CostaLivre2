# Costa Livre 2 · atualização de sistemas

## Como testar

- **Banco:** use um caixa perto da delegacia, hotel ou loja de informática em Santa Luz, ou na área central das três cidades da rodovia. Deposite e saque, confira o histórico ou mova dinheiro entre as contas pessoal e empresarial. Em LAN, dois jogadores podem transferir entre si, inclusive em regiões diferentes. No modo solo, quem possui uma Vila pode ativar o débito diário no caixa. Em LAN, um policial em atividade recebe o salário na conta pessoal a cada cinco minutos.
- **Oficina:** leve um veículo compatível à baia da Oficina Costeira em Santa Luz (perto de -40, 51 no mapa) ou à Garagem do Cais. Pare e use a interação para inspecionar as peças. Alterne entre pagamento em espécie e saldo bancário. Confira a preparação novamente após reiniciar em solo ou reconectar ao mesmo anfitrião em LAN. A configuração em uso pertence ao veículo da sala. Quem comprou uma configuração pode reaplicá-la ao assumir esse veículo em uma sala nova hospedada na mesma instalação.
- **Campanha:** abra o mapa e selecione **Diário**, ou vá ao marcador da Lia. Escolha entre Lia, Rui e Célia. Siga os locais no mapa. O diário exibe as decisões e a confiança dos três personagens. A entrega de mercadoria exige veículo. O acordo da cooperativa desbloqueia compra direta de níveis avançados na oficina. Cada escolha aceita salva o próximo checkpoint e a recompensa em uma única gravação.
- **Rede:** hospede e entre com outra instância da mesma versão do jogo. Afaste uma instância por mais de 155 m e volte. No painel Multiplayer, compare a quantidade de entidades, taxa de dados recebidos e RTT. O elenco e os avisos globais permanecem disponíveis fora desse raio.
- **Desempenho:** em Opções → Gráficos, escolha **BATATA EXTREMO** ou personalize um preset. Trave escala ou ajustes de mundo separadamente. Em Tela, selecione 30, 60, 90 ou 120 FPS. Em desktop, VSync pode determinar a cadência efetiva. Os números no menu são da coleta mais recente durante a partida.

## Arquitetura e limites

- O banco usa valores inteiros, grava arquivo temporário com flush, troca o arquivo de dados e mantém a versão anterior. As transações têm identificadores e as solicitações relevantes têm sequência por identidade. O anfitrião mantém o registro LAN e as identidades usam a autenticação de sessão existente. Uma sala hospedada por outra instalação tem seu próprio livro de contas.
- A versão solo importa o saldo legado ao criar a conta e mantém o arquivo de economia separado do registro LAN. Alguns ativos anteriores, como propriedade e mochila, ainda usam o save legado.
- O roteiro fica em `Assets/HarborPrototype/Resources/Harbor/Campaign.json`. Identificadores de checkpoints e escolhas são persistentes. Cada nó aponta para a próxima etapa via escolhas com condições, recompensas e alterações de confiança. O carregamento valida destinos, IDs repetidos, nós isolados e quantidade de finais. Novos locais precisam ser associados a uma âncora em `CostaCampaign`.
- A oficina monta módulos sobre os veículos existentes e usa MaterialPropertyBlock nos materiais compartilhados. Os principais cálculos mecânicos passam pelos mesmos multiplicadores nas instâncias remota e local. A pintura perolada usa o material existente e a faixa secundária é um módulo, sem editor livre de adesivos.
- LAN, Noray e WebSocket continuam usando o transporte anterior. As entidades dinâmicas próximas são selecionadas por posição, com varredura linear na escala atual e grade para volumes maiores. Veículos estacionados enviam estado quando mudam ou na atualização periódica. O protocolo 14 exige a mesma versão de ambos os lados. Trânsito ambiente e informação global continuam em snapshots. A reconciliação de movimento existente permanece.
- O perfil Batata Extremo reduz a escala, a distância de visão e o orçamento visual. O controlador usa histerese e mantém colisões e regras do jogo. A memória exibida é a alocação medida pela Unity, sem equivalência direta ao RSS do processo. A leitura GPU depende do backend.
- A paralelização em produção continua pendente de ganho comprovado. O ensaio foi com varredura paralela gerenciada, sem tratar esse resultado como uma medição de Burst. Os sistemas de tráfego e NPC ainda chamam a física da Unity na thread principal.

## Validação realizada

- Compilação C# dos scripts do protótipo com as referências do projeto Unity 6000.3.29f1.
- Teste isolado de 32 operações concorrentes com a mesma sequência, transferência única, limites de saldo, recarga do disco e recuperação da cópia anterior.
- Teste dos canais de customização, compatibilidade de motos e multiplicadores de física. Teste de cinco rotas completas da campanha com persistência, retomada e rejeição de repetição.
- Teste Unity LAN em fixture isolada com dois participantes. Passaram entrada, relevância nas duas regiões, RTT, depósito, compra de motor com saldo pessoal, checkpoint e reconexão com os estados preservados. As posições do roteiro foram controladas no fixture, portanto o teste não substitui a inspeção visual do caminho no jogo.
- Teste sintético do controlador a 60 FPS simulou gargalos isolados de CPU e GPU, tempo de espera entre ajustes e recuperação gradual.

### Microbenchmarks

Medianas em milissegundos, 16 consultas por iteração e 80 amostras. O tempo da grade inclui a reconstrução e as consultas. São operações puras em dados sintéticos, sem renderização ou transporte.

| Entidades | Varredura direta | Grade espacial | Varredura paralela gerenciada |
| --- | ---: | ---: | ---: |
| 33 | 0,0375 | 0,0480 | 0,0509 |
| 4.096 | 4,1098 | 1,9090 | 3,4331 |
| 32.768 | 34,8047 | 15,4589 | 23,9565 |

Em um exemplo sintético com 16 carros, 10 deles relevantes, o JSON isolado da frota passou de 1.774 B no formato completo para 1.185 B na entrada e 67 B em um snapshot sem mudanças. Na janela de 18 snapshots com um envio completo e 17 estacionários, foram 129,1 B em média. Isso exclui os outros estados e os cabeçalhos, e não é uma captura de tráfego real.

A comparação de FPS, CPU, GPU e RAM do jogo inteiro antes/depois ainda precisa ser medida com executáveis equivalentes. Compare o mesmo percurso, resolução, preset, conta, densidade e clima, com VSync desativado, em pelo menos três execuções e registre a mediana e os percentis. O microbenchmark não prova 60 FPS na Intel HD 4400.
