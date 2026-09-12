# PRÓXIMAS TAREFAS

NÃO puxe trabalhos distantes e enormes. Foque na manutenção da estabilidade do código atual. Respeite as prioridades antes de avançar de categoria.

### P0 (Erros Críticos / Bugs / Regressões a arrumar)
- Performance dos POIs / lixo do chunk generator ao longo do Save.
- Mouse dessincroniza ao alternar rápido Deck/Cartas (Captured/Visible).

### P1 (Mudança de Arquitetura - Interiores 2D)
- **Implementar o InteriorController.cs:** Interiores não serão mais mundos 3D. Quando o jogador entrar em uma Taverna, Cabana ou Loja, o jogo carregará uma Tela 2D Imersiva (Visual Novel/Point and Click) usando a arte correspondente em Assets/ArtKit/Interiors/.
- **Benefícios:** Elimina bugs de colisão/navmesh em espaços fechados, facilita o trabalho procedural e eleva a beleza visual para padrão RPG de Mesa/Darkest Dungeon.
- **Interações:** Adicionar botões na tela 2D para interagir com o ambiente (ex: "Dormir", "Conversar com Mercador", "Sair").

### P2 (Melhorias Visuais e Áudio)
- Animais passivos no mundo (pássaros, coelhos).
- Áudio no combate (efeitos sonoros para as cartas, dano e botões).

### P3 (Futuras Ideias Conceptuais)
- Companheiros jogando cartas sozinhos em batalhas multi-turno.
