# HexManiac Mobile — primeiro protótipo

Este fork acrescenta `src/HexManiac.Android`, um teste funcional do Core original no Android.
Abre uma ROM com o seletor do sistema, inicializa `PokemonModel`, altera um byte por offset hexadecimal e exporta uma cópia. Não inclui ainda os editores de tabelas, mapas, scripts ou interface Avalonia. Mantenha ROMs originais fora do repositório.

## Desenvolvimento pelo telefone

1. Crie um fork de `haven1433/HexManiacAdvance` no GitHub e aplique estes arquivos à sua branch.
2. No Termux, edite e faça `git push` para disparar **Actions → Android APK**.
3. Baixe o artifact `HexManiac-Mobile-debug` na execução concluída, extraia o ZIP e instale o APK.
4. Abra uma cópia de uma ROM própria, edite um byte conhecido e salve como outro arquivo. Compare os arquivos e confira se apenas esse byte mudou.

A compilação requer .NET 10 e o workload Android em um runner Linux x86-64. O Core original permanece `net6.0`, referenciado pelo app `net10.0-android`. O projeto usa `SolutionDir` porque o Core consulta essa propriedade para incluir `SharedAssemblyInfo.cs`. O APK de debug é para testes locais e não é um release assinado para distribuição.

## Próximas etapas

- Validar o APK e a inicialização em ROMs de teste sem dados privados.
- Implementar navegação hex, seleção por toque, goto e undo/redo.
- Definir e implementar a interface final (Avalonia ou controles Android) após medir tamanho, desempenho e integração real do Core.
- Integrar metadados, tabelas e diálogos do Core; por último imagens, scripts e mapas.

O código original mantém sua licença MIT (`LICENSE`).
