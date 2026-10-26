# Publicar o Cooler Panel

## Preparação local

O projeto contém README público, licença MIT, fontes, ícone, layout de exemplo, instruções do RTSS, script de compilação e script de empacotamento. A versão inicial é **0.1.0**.

Execute `tools/Package.ps1` no Windows. Ele gera:

- `dist/CoolerPanel-0.1.0-win-x64.zip`: executável, licença, instruções e exemplo;
- `dist/CoolerPanel-0.1.0-source.zip`: somente os arquivos públicos de código-fonte;
- `dist/SHA256SUMS.txt`: hashes dos dois pacotes. O ZIP de distribuição também contém o hash do executável.

O script não publica nada e não interrompe a cópia já em execução. Os testes locais não usam sensores/USB; o funcionamento físico foi validado separadamente na unidade descrita no README.

## Criar o repositório

1. Defina a conta/organização e o nome do repositório público, por exemplo `cooler-panel`.
2. Extraia o ZIP de código-fonte para uma pasta nova. Essa pasta contém somente a seleção pública e é a forma mais simples de evitar enviar arquivos da investigação local.
3. Crie o repositório e envie os arquivos extraídos. A licença já está incluída; não gere outra pelo assistente do GitHub.
4. Revise o conteúdo antes do primeiro envio: nenhum executável/driver do fabricante, configuração pessoal do RTSS, log local ou caminho de usuário deve estar incluído.
5. Crie uma release marcada como **pré-lançamento**, tag sugerida `v0.1.0`, por ser a primeira versão pública e ainda ter validação limitada a uma unidade.
6. Anexe o ZIP Windows e `SHA256SUMS.txt`. O ZIP de fontes preparado também pode ser anexado; o GitHub já oferece arquivos dos fontes do próprio repositório.

Descrição sugerida do repositório:

> Atualiza o mostrador USB de coolers compatíveis com dados do RivaTuner. Testado no Husky Glacier HWT700PT. Windows x64.

Não anuncie suporte universal a Husky, a todos os coolers que usam CPU COOLER V3.0, ou a qualquer dispositivo com `AA88:8666`. A tabela do README distingue o hardware confirmado dos casos ainda não verificados.

## O que ainda precisa de validação

- Download e extração numa segunda máquina; .NET e RTSS disponíveis nesse ambiente.
- Importação/renderização do layout de exemplo no OverlayEditor sem depender de uma configuração pessoal.
- Novo login do Windows com ambos os programas abrindo automaticamente.
- Outras versões do RTSS, outras revisões do HWT700PT e outros modelos: somente após testes ou relatos acompanhados de identificação.

O executável permanece sem assinatura digital. Assinatura de código e automação de compilação são melhorias opcionais; não foram apresentadas como garantias contra detecções de antivírus ou como itens já implementados.
