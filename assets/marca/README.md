# Marca — arquivos-fonte

Arte original do AzurePrep. **Nada aqui é servido pela aplicação**: estes arquivos existem
para gerar os derivados que ficam em `src/AzurePrep.Web/wwwroot/`.

| Arquivo | Formato | Papel |
|---|---|---|
| `logo-azureprep-transparente.png` | 500×500 RGBA | **Fonte em uso.** Origem de todos os derivados |
| `logo-azureprep-fundo-branco.jpg` | 5000×5000 | Arte original, fundo branco chapado. Maior resolução, mas sem canal alpha |

## Regenerar os derivados

```bash
./assets/marca/gerar-derivados.sh
```

Produz, em `wwwroot/`:

- `favicon.ico` — 16/32/48 embutidos, atende o pedido automático do browser por `/favicon.ico`
- `img/icone-{16,32,180,192,512}.png` — favicons, `apple-touch-icon` e a marca do cabeçalho
- `img/logo-azureprep.png` — logo da tela de login (360px, exibida a 180px)

Os derivados são gerados, não editados à mão: qualquer ajuste manual some na próxima execução.

## Por que ficam fora de `wwwroot/`

Servir a fonte não faria sentido — o JPG sozinho tem 796 KB contra os 110 KB do PNG derivado
que a tela de login realmente usa. Também não podem morar em `Views/`, onde estavam antes:
ali só os `.cshtml` são compilados, então imagens naquela pasta ficam inacessíveis pela web.
