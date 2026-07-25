#!/usr/bin/env bash
#
# Regenera os arquivos de marca servidos em wwwroot/ a partir das fontes desta pasta.
# Rode depois de trocar a arte original — os derivados NÃO devem ser editados à mão.
#
# Requer apenas ffmpeg e python3 (a máquina de desenvolvimento não tem ImageMagick
# nem Pillow, por isso o .ico é montado byte a byte mais abaixo).
#
set -euo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WWWROOT="$(cd "$AQUI/../.." && pwd)/src/AzurePrep.Web/wwwroot"
FONTE="$AQUI/logo-azureprep-transparente.png"

# Recorte da marca (nuvem + "A" + checklist) dentro da arte de 500x500, depois centrado
# num quadrado transparente. Os textos "AZUREPREP APP" e "SIMULADOS PARA EXAMES AZURE"
# ficam de fora de propósito: viram borrão ilegível em 16px.
MARCA="crop=350:250:75:35,pad=350:350:0:50:color=#00000000"

mkdir -p "$WWWROOT/img"

# Ícones quadrados. O 48 só existe para entrar no .ico e é descartado no final.
for TAMANHO in 16 32 48 180 192 512; do
    ffmpeg -y -v error -i "$FONTE" \
        -vf "$MARCA,scale=$TAMANHO:$TAMANHO:flags=lanczos" \
        "$WWWROOT/img/icone-$TAMANHO.png"
done

# Logo completa da tela de login: exibida a 180px, gerada em 2x para telas densas.
ffmpeg -y -v error -i "$FONTE" -vf "scale=360:360:flags=lanczos" \
    "$WWWROOT/img/logo-azureprep.png"

# favicon.ico multi-resolução. O formato aceita PNG embutido desde o Vista, então basta
# o cabeçalho ICONDIR (6 bytes) + uma ICONDIRENTRY (16 bytes) por imagem + os blobs.
python3 - "$WWWROOT" <<'PYTHON'
import struct
import sys

wwwroot = sys.argv[1]
tamanhos = [16, 32, 48]

pngs = []
for tamanho in tamanhos:
    with open(f"{wwwroot}/img/icone-{tamanho}.png", "rb") as arquivo:
        pngs.append((tamanho, arquivo.read()))

cabecalho = struct.pack("<HHH", 0, 1, len(pngs))
deslocamento = len(cabecalho) + 16 * len(pngs)

entradas, blobs = b"", b""
for tamanho, dados in pngs:
    entradas += struct.pack(
        "<BBBBHHII",
        tamanho if tamanho < 256 else 0,  # largura (0 significa 256)
        tamanho if tamanho < 256 else 0,  # altura
        0,                                # paleta indexada: nenhuma
        0,                                # reservado
        1,                                # planos de cor
        32,                               # bits por pixel
        len(dados),
        deslocamento,
    )
    blobs += dados
    deslocamento += len(dados)

with open(f"{wwwroot}/favicon.ico", "wb") as arquivo:
    arquivo.write(cabecalho + entradas + blobs)

print(f"favicon.ico: {len(cabecalho + entradas + blobs)} bytes, {len(pngs)} resolucoes")
PYTHON

rm -f "$WWWROOT/img/icone-48.png"

echo "Derivados regenerados em $WWWROOT"
