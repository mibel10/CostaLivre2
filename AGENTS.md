# Instruções do projeto Costa Livre 2

## Segurança no terminal

- Use aspas em todos os caminhos passados ao shell, inclusive nas expansões de variáveis que contêm caminhos. Exemplo: `cd -- "/home/mibel/Costa Livre 2"`.

## Compatibilidade com o multiplayer

Planeje toda mudança considerando como ela afeta o multiplayer. Ao alterar ações de jogadores ou estado compartilhado, preserve a validação pelo anfitrião e a sincronização entre os clientes. Verifique o comportamento alterado em uma sessão com anfitrião e cliente quando a mudança afetar a partida em rede.

## Commits e envio ao GitHub

Faça um commit para cada mudança concluída. Use uma mensagem curta e específica que diga o que mudou, como `Corrige sincronização da mochila`. Separe mudanças independentes em commits próprios.

Envie cada commit ao repositório [mibel10/CostaLivre2](https://github.com/mibel10/CostaLivre2) com `git push`. A mudança só está concluída quando o envio ao GitHub for confirmado.
