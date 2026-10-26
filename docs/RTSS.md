# Configurar as leituras no RivaTuner

O Cooler Panel lê o texto numérico publicado pelo **OverlayEditor** do RTSS, não a imagem desenhada na tela e não a memória privada do processo. O formato de memória compartilhada usado foi conferido no SDK fornecido com o RTSS, em `SDK/Include/RTSSSharedMemory.h`.

## Usar um layout existente

1. Nas configurações do RTSS, ative o plugin **OverlayEditor**.
2. Abra o editor e confira se o layout ativo tem as fontes `CPU temperature` e `GPU1 temperature`, com unidade **°C** e sem fórmula de conversão.
3. Cada temperatura precisa aparecer em uma camada de texto própria: `%CPU temperature%` ou `%GPU1 temperature%`. Formatação como `<S=100>` pode envolver a referência. Coloque legendas e unidades em camadas separadas, para que a camada numérica contenha só um valor.
4. Para enviar utilização, crie também camadas separadas `%CPU usage%` e `%GPU1 usage%`, com unidades `%` e sem fórmulas.
5. Salve o layout e deixe o OverlayEditor ativo. Clique em **Ler temperaturas** no Cooler Panel para verificar o resultado antes de enviar ao painel.

Não é obrigatório usar MSI Afterburner. No computador testado, o próprio HAL do RTSS já oferece as temperaturas necessárias. Se as fontes não estiverem disponíveis no seu hardware, o layout de exemplo não conseguirá criá-las: a disponibilidade depende do RTSS e de seus componentes de coleta.

## Layout de exemplo

O arquivo `examples/CoolerPanel.ovl` foi criado para este projeto, com quatro fontes HAL e camadas separadas para legendas e valores.

1. Guarde uma cópia do seu layout atual.
2. Pelo OverlayEditor, abra o arquivo de exemplo e salve-o como layout ativo na pasta de overlays do RTSS. Copiar para `Plugins\Client\Overlays` pode exigir permissão de administrador; prefira a operação de abrir/salvar do editor.
3. Confirme que as temperaturas aparecem e se atualizam. O Cooler Panel descobre o layout ativo por `Plugins\Client\OverlayEditor.cfg`; apenas copiar o arquivo sem ativá-lo não basta.
4. Inicie o monitoramento no Cooler Panel.

O exemplo foi verificado pelo parser e pelo mapeamento de fontes do projeto. Sua abertura/renderização no OverlayEditor ainda precisa de confirmação em outras instalações. O layout pessoal usado no teste original não é distribuído.

O `.ovl` está em **Windows-1252**, formato usado pelos arquivos de configuração examinados do RTSS. Preserve essa codificação ao editar; mudar a representação do símbolo `°` pode impedir o reconhecimento da unidade.

## O painel fica sem atualizar

- Mantenha RTSS e OverlayEditor abertos, inclusive fora dos jogos, e confira se o layout continua sendo publicado.
- Feche o programa original do cooler; minimizar sua janela pode não liberar o USB.
- Se não houver temperaturas na janela do Cooler Panel, resolva primeiro a fonte no RTSS.
- Se o painel for detectado mas não exibir corretamente, registre modelo/revisão e identificadores. Não conclua que outro cooler é compatível apenas pelo formato físico ou pelo nome do programa original.
