# Cooler Panel

Aplicativo leve para Windows que atualiza o mostrador USB de temperatura de coolers compatíveis, aproveitando as leituras já publicadas pelo **RivaTuner Statistics Server (RTSS)**.

**Testado em um Water Cooler Husky Glacier ARGB 360 mm, preto, modelo HWT700PT**, com painel USB `AA88:8666`. O funcionamento com temperaturas reais foi confirmado pelo proprietário da unidade. [Referência do modelo na KaBuM!](https://www.kabum.com.br/produto/593864/water-cooler-husky-glacier-iluminacao-argb-360mm-amd-e-intel-preto-hwt700pt).

Projeto independente, sem vínculo com Husky, KaBuM!, RTSS ou os fabricantes de hardware. Versão inicial **0.1.0**; a compatibilidade ainda se baseia em uma unidade testada.

## Recursos

- Temperaturas e utilização de CPU/GPU enviadas ao mostrador aproximadamente uma vez por segundo.
- Execução na bandeja, instância única e inicialização opcional ao entrar no Windows.
- Reconexão automática quando o RivaTuner ou o painel ainda não está disponível.
- Botões para consultar sensores, detectar o painel e enviar valores de teste.
- Um executável pequeno, sem DLLs adicionais ou instalação de driver próprio.

O programa controla os dados do **mostrador**. Não regula bomba, ventoinhas, ARGB ou overclock. Memória, potência e rotação não foram integradas e seus campos são enviados como zero.

## Compatibilidade

| Item | Situação |
| --- | --- |
| Husky Glacier 360 mm HWT700PT, unidade testada | Confirmado pelo proprietário |
| USB HID `VID_AA88&PID_8666`, saída de 11 bytes | Protocolo verificado nessa unidade |
| Outros coolers com o mesmo identificador | Não testados; o identificador sozinho não garante o mesmo protocolo |
| Outras versões, tamanhos ou revisões do Husky Glacier | Não verificados |
| Windows x64 | Testado em um computador Windows x64, build 26200 |
| RTSS 7.3.5.28314 com OverlayEditor e fontes HAL | Testado; outras versões precisam de confirmação |
| Linux, macOS, Windows x86/ARM64 nativo | Não suportados nesta compilação |

A compatibilidade de sockets AMD/Intel anunciada para o cooler é mecânica e não comprova compatibilidade deste software com outros painéis. Para reportar outro modelo, use o modelo exato, revisão, identificador USB, tamanho do relatório e resultado observado.

## Requisitos

1. Windows x64 com **.NET Framework 4.8**, recomendado para esta distribuição. Outras combinações de Windows/.NET não foram validadas.
2. [RivaTuner Statistics Server, distribuído pela Guru3D](https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/), instalado na pasta padrão `C:\Program Files (x86)\RivaTuner Statistics Server`.
3. OverlayEditor ativo e publicando temperaturas da CPU e GPU em °C. O RTSS precisa disponibilizar essas leituras no computador do usuário.
4. Painel USB compatível conectado; o software original deve estar fechado para liberar o dispositivo.

**O RTSS é uma dependência obrigatória desta versão.** O programa não lê sensores físicos diretamente. Ele próprio não carrega WinRing0 nem instala drivers; os componentes de coleta de sensores do RTSS são gerenciados pelo próprio RTSS.

## Usar

1. Baixe o ZIP de distribuição e extraia tudo para uma pasta permanente, por exemplo dentro de Documentos. Não execute de dentro do ZIP.
2. Configure o RTSS seguindo [o guia do OverlayEditor](docs/RTSS.md). Se já usa um layout compatível, pode mantê-lo.
3. Saia do aplicativo original do cooler e seus auxiliares.
4. Abra `CoolerPanel.exe`. O monitoramento começa automaticamente.
5. Confirme os valores na janela e no mostrador físico.

Fechar ou minimizar recolhe a janela para a bandeja e mantém os envios. Dê dois cliques no ícone para reabrir. No menu da bandeja, **Sair** encerra o programa. **Parar** suspende o monitoramento e **Iniciar** retoma.

Marque **Iniciar com o Windows** para registrar somente este usuário. Essa opção começa desativada para novos usuários: nenhum instalador a ativa por conta própria. Se mudar o executável de pasta, desative e ative novamente a opção na nova localização. O RTSS também precisa estar configurado para abrir no login; o Cooler Panel aguarda sua disponibilidade, mas não configura a inicialização de outros programas.

**Enviar teste** envia CPU 42 °C e GPU 37 °C por padrão: são valores simulados. Os dois campos de teste correspondem a CPU e GPU, nessa ordem. Esse botão serve para diagnóstico, não para medir o computador.

## Limitações conhecidas

- RTSS fora da pasta padrão não é localizado automaticamente nesta versão.
- O layout precisa de camadas escalares para `CPU temperature` e `GPU1 temperature`, sem fórmula de conversão e com unidades `°C`.
- Utilização CPU/GPU é opcional; quando a camada correspondente não está disponível, seu campo é enviado como zero.
- O contador de quadros do RTSS é usado para detectar interrupção do overlay. Ele não permite comprovar independentemente a atualização de cada sensor.
- Em falhas, novos envios são suspensos e uma nova tentativa ocorre após cinco segundos. O mostrador pode continuar exibindo o último valor recebido; não o use como proteção contra superaquecimento.
- O executável não possui assinatura digital. Avisos de reputação podem aparecer. Não é necessário desativar o antivírus para usar este projeto; uma detecção deve ser investigada pelo nome e pelo hash do arquivo.

## Compilar e testar

No Windows, a partir da raiz do projeto:

```powershell
& .\tools\Build.ps1
$test = Start-Process -FilePath .\app\CoolerPanel.exe -ArgumentList '--self-test' -Wait -PassThru
Get-Content .\app\resultado-teste.txt
if ($test.ExitCode -ne 0) { throw 'Testes falharam.' }
```

O script usa o compilador do .NET Framework em `Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. Respeite a política de execução de scripts do seu Windows; não é necessário alterá-la globalmente. Alternativa direta:

```powershell
New-Item -ItemType Directory -Force app
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /win32icon:assets\cooler-panel.ico /out:app\CoolerPanel.exe src\*.cs
```

Não é necessário Python para compilar ou executar. Somente a regeneração opcional do ícone com `tools/Build-Icon.py` requer Python e Pillow; o ícone pronto e sua fonte vetorial já estão em `assets/`.

Para criar os pacotes de distribuição e de código-fonte, use `tools/Package.ps1`. Ele compila em uma pasta separada, testa o executável e usa uma lista explícita de arquivos públicos. Não modifica a instalação em execução.

## Diagnóstico

| Opção | Ação |
| --- | --- |
| `--self-test` | Testes locais, sem sensores ou USB |
| `--probe` | Consultar o painel, sem enviar |
| `--sensors` | Ler o overlay, sem enviar |
| `--send-test` | Enviar valores simulados uma vez |
| `--live-test` | Enviar 15 atualizações reais |
| `--tray` | Iniciar oculto, com monitoramento |
| `--startup-enable` | Ativar inicialização no login para o caminho atual |
| `--startup-status` | Consultar configuração de inicialização |

Os comandos de diagnóstico gravam `resultado-teste.txt` junto ao executável. Execute-os numa pasta em que seu usuário possa escrever. Os testes de hardware exigem o painel correto; o teste local não comprova compatibilidade de um novo cooler.

Para remover, desmarque **Iniciar com o Windows**, escolha **Sair** e apague a pasta. Nenhum serviço ou driver deste projeto precisa ser desinstalado.

## Código aberto

Código, ícone e layout de exemplo deste projeto sob [licença MIT](LICENSE). RTSS, Windows e .NET não são distribuídos neste repositório e mantêm suas próprias licenças. Não são incluídos executáveis, drivers, DLLs ou imagens do fabricante do cooler.
