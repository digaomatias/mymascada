**Idioma / Language:** [English](SELF-HOSTING.md) | Portugues (BR)

# Self-Hosting do MyMascada

Guia para implantar o MyMascada no seu proprio servidor usando Docker.

---

## Inicio Rapido

### Pre-requisitos

- Docker Engine 24+ com Docker Compose v2
- 1 GB de RAM minimo (2 GB recomendado)
- `openssl` instalado (para gerar segredos)

### Passos

1. **Clone o repositorio**

   ```bash
   git clone https://github.com/digaomatias/mymascada.git
   cd mymascada
   ```

2. **Execute o script de configuracao**

   ```bash
   chmod +x setup.sh
   ./setup.sh
   ```

   O script gera senhas seguras, guia voce pela configuracao e inicia a aplicacao.
   Alternativamente, copie `.env.example` para `.env` e edite manualmente.

3. **Abra a aplicacao**

   Acesse `http://localhost:3000` no navegador e crie sua primeira conta.

---

## Requisitos

| Requisito | Minimo | Recomendado |
|---|---|---|
| Docker Engine | 24+ | Ultima versao estavel |
| Docker Compose | v2 | Ultima versao estavel |
| RAM | 1 GB | 2 GB |
| Disco | 1 GB | 5 GB+ (depende do volume de transacoes) |
| SO | Linux, macOS ou Windows com WSL2 | Linux (Debian/Ubuntu) |
| Nome de dominio | Opcional | Necessario para HTTPS |

---

## Referencia de Configuracao

Toda configuracao e feita atraves de variaveis de ambiente no arquivo `.env`. Execute
`./setup.sh` para configuracao guiada, ou copie `.env.example` e edite manualmente.

### Banco de Dados

| Variavel | Obrigatoria | Descricao | Padrao |
|---|---|---|---|
| `DB_USER` | Sim | Usuario do PostgreSQL | `mymascada` |
| `DB_PASSWORD` | Sim | Senha do PostgreSQL. O script de configuracao gera uma automaticamente. | -- |
| `DB_NAME` | Sim | Nome do banco de dados PostgreSQL | `mymascada` |

### Autenticacao JWT

| Variavel | Obrigatoria | Descricao | Padrao |
|---|---|---|---|
| `JWT_KEY` | Sim | Chave secreta para assinatura de tokens JWT. Deve ter pelo menos 32 caracteres. Gere com `openssl rand -base64 64`. | -- |
| `JWT_ISSUER` | Nao | Claim issuer do JWT | `MyMascada` |
| `JWT_AUDIENCE` | Nao | Claim audience do JWT | `MyMascadaUsers` |

### URLs da Aplicacao

| Variavel | Obrigatoria | Descricao | Padrao |
|---|---|---|---|
| `FRONTEND_URL` | Sim | URL publica onde os usuarios acessam o frontend (sem barra no final) | `http://localhost:3000` |
| `FRONTEND_PORT` | Nao | Porta do host mapeada para o container do frontend. Extraida de `FRONTEND_URL` pelo `setup.sh`. O container sempre escuta na porta 3000 internamente. | `3000` |
| `API_URL` | Nao | URL interna que o container do frontend usa para acessar a API. No Docker Compose, e o nome do servico. Altere apenas se modificar a configuracao de rede. | `http://api:5126` |
| `PUBLIC_API_URL` | Sim | URL publica que os navegadores usam para acessar a API. Para acesso local: `http://localhost:5126`. Ao usar proxy reverso (Caddy ou Nginx), defina como o mesmo dominio de `FRONTEND_URL`, pois o proxy roteia `/api/*` para a API. | `http://localhost:5126` |
| `CORS_ALLOWED_ORIGINS` | Sim | Lista de origens permitidas separadas por virgula. Deve incluir `FRONTEND_URL`. | `http://localhost:3000` |

### Acesso Beta

| Variavel | Obrigatoria | Descricao | Padrao |
|---|---|---|---|
| `BETA_REQUIRE_INVITE_CODE` | Nao | Defina como `true` para exigir codigos de convite em novos cadastros | `false` |
| `BETA_VALID_INVITE_CODES` | Nao | Lista de codigos de convite validos separados por virgula | -- |

### Categorizacao por IA (OpenAI)

| Variavel | Obrigatoria | Descricao | Padrao |
|---|---|---|---|
| `OPENAI_API_KEY` | Nao | Chave da API OpenAI para categorizacao automatica de transacoes | -- |
| `OPENAI_MODEL` | Nao | Modelo OpenAI a ser utilizado | `gpt-4o-mini` |

### Google OAuth

| Variavel | Obrigatoria | Descricao | Padrao |
|---|---|---|---|
| `GOOGLE_CLIENT_ID` | Nao | ID do cliente Google OAuth | -- |
| `GOOGLE_CLIENT_SECRET` | Nao | Segredo do cliente Google OAuth | -- |

Configure o URI de redirecionamento autorizado no Google Cloud Console como:
`{FRONTEND_URL}/api/auth/google-callback`

### Sincronizacao Bancaria (Akahu)

| Variavel | Obrigatoria | Descricao | Padrao |
|---|---|---|---|
| `AKAHU_ENABLED` | Nao | Defina como `true` para habilitar a sincronizacao bancaria via Akahu. Cada usuario insere seus proprios tokens via Configuracoes. | `false` |
| `AKAHU_APP_SECRET` | Nao | Segredo da aplicacao Akahu -- necessario apenas para o fluxo OAuth de Production App | -- |

### Notificacoes por Email

| Variavel | Obrigatoria | Descricao | Padrao |
|---|---|---|---|
| `EMAIL_ENABLED` | Nao | Habilitar funcionalidades de email | `false` |
| `EMAIL_PROVIDER` | Nao | Provedor de email: `smtp` ou `postmark` | `smtp` |
| `EMAIL_FROM_ADDRESS` | Nao | Endereco de email do remetente | `noreply@example.com` |
| `EMAIL_FROM_NAME` | Nao | Nome de exibicao do remetente | `MyMascada` |
| `SMTP_HOST` | Nao | Hostname do servidor SMTP | -- |
| `SMTP_PORT` | Nao | Porta do servidor SMTP | `587` |
| `SMTP_USERNAME` | Nao | Usuario de autenticacao SMTP | -- |
| `SMTP_PASSWORD` | Nao | Senha de autenticacao SMTP | -- |
| `SMTP_USE_STARTTLS` | Nao | Usar criptografia STARTTLS | `true` |
| `SMTP_USE_SSL` | Nao | Usar SSL implicito | `false` |
| `POSTMARK_SERVER_TOKEN` | Nao | Token do servidor Postmark (quando `EMAIL_PROVIDER=postmark`) | -- |
| `POSTMARK_MESSAGE_STREAM` | Nao | Stream de mensagens do Postmark | `outbound` |

### Proxy Reverso

| Variavel | Obrigatoria | Descricao | Padrao |
|---|---|---|---|
| `DOMAIN` | Nao | Nome de dominio para o proxy Caddy integrado. O Caddy provisiona HTTPS automaticamente via Let's Encrypt quando um dominio real e configurado. | `localhost` |

---

## Funcionalidades Opcionais

O MyMascada funciona imediatamente com apenas a configuracao obrigatoria. Cada
funcionalidade opcional adiciona capacidades, mas nao e necessaria para a operacao
principal.

**Categorizacao por IA (OpenAI)** -- Categoriza automaticamente transacoes importadas
usando OpenAI. Sem ela, use categorizacao manual e regras automaticas, que estao
sempre disponiveis.

**Google OAuth** -- Adiciona o botao "Entrar com Google" na pagina de login. Sem
ele, a autenticacao por email e senha esta sempre disponivel.

**Sincronizacao Bancaria (Akahu)** -- Sincronizacao automatica de contas bancarias
da Nova Zelandia via API do Akahu. Sem ela, importe transacoes manualmente usando
arquivos CSV ou OFX.

**Notificacoes por Email** -- Habilita emails de recuperacao de senha, verificacao
de email e entrega de notificacoes. Sem ela, a aplicacao ainda funciona, mas a
recuperacao de senha requer intervencao direta no banco de dados por um administrador.

---

## Deploy em Producao (HTTPS)

Para uso em producao, voce deve servir a aplicacao via HTTPS. Duas opcoes sao
fornecidas.

### Opcao 1: Caddy integrado (recomendado)

O Caddy esta incluido no arquivo Docker Compose como um perfil opcional. Ele
provisiona e renova certificados SSL automaticamente via Let's Encrypt.

1. Configure seu dominio no `.env`:

   ```
   DOMAIN=financas.exemplo.com
   FRONTEND_URL=https://financas.exemplo.com
   PUBLIC_API_URL=https://financas.exemplo.com
   CORS_ALLOWED_ORIGINS=https://financas.exemplo.com
   ```

   `PUBLIC_API_URL` deve ser igual a `FRONTEND_URL` porque o Caddy roteia
   requisicoes `/api/*` para o container da API atraves do mesmo dominio.

2. Certifique-se de que as portas 80 e 443 estao abertas no servidor e que o
   registro DNS A aponta para o IP publico do servidor.

3. Inicie com o perfil proxy:

   ```bash
   docker compose --profile proxy up -d --build
   ```

O Caddy cuida do provisionamento de certificados SSL, renovacao e redirecionamento
HTTP-para-HTTPS automaticamente. Tanto a API (`/api/*`) quanto o frontend (`/`) sao
servidos atraves de um unico dominio.

### Opcao 2: Nginx externo

Um exemplo de configuracao Nginx e fornecido em `deploy/nginx.conf.example`.

1. Copie o arquivo de exemplo:

   ```bash
   sudo cp deploy/nginx.conf.example /etc/nginx/sites-available/mymascada
   sudo ln -s /etc/nginx/sites-available/mymascada /etc/nginx/sites-enabled/
   ```

2. Edite o arquivo e substitua `finance.example.com` pelo seu dominio.

3. Configure a URL publica da API no `.env` para corresponder ao seu dominio
   (o Nginx roteia `/api/*` para a API):

   ```
   FRONTEND_URL=https://financas.exemplo.com
   PUBLIC_API_URL=https://financas.exemplo.com
   CORS_ALLOWED_ORIGINS=https://financas.exemplo.com
   ```

4. Obtenha certificados SSL com certbot:

   ```bash
   sudo certbot --nginx -d financas.exemplo.com
   ```

5. Inicie a aplicacao sem o perfil Caddy:

   ```bash
   docker compose up -d --build
   ```

   O Nginx faz proxy para as portas 5126 (API) e 3000 (frontend) no host.

6. Recarregue o Nginx:

   ```bash
   sudo nginx -t && sudo systemctl reload nginx
   ```

---

## Atualizacao

Baixe as ultimas alteracoes e reconstrua os containers. As migracoes do banco de
dados sao executadas automaticamente na inicializacao.

```bash
git pull
docker compose up -d --build
```

Se estiver usando o proxy Caddy:

```bash
git pull
docker compose --profile proxy up -d --build
```

---

## Backup e Restauracao

### Backup do Banco de Dados

```bash
docker compose exec postgres pg_dump -U mymascada mymascada > backup_$(date +%Y%m%d).sql
```

### Restauracao do Banco de Dados

```bash
docker compose exec -T postgres psql -U mymascada mymascada < backup.sql
```

### Volumes Docker

Os seguintes volumes Docker contem dados persistentes:

| Volume | Conteudo |
|---|---|
| `postgres-data` | Arquivos do banco de dados PostgreSQL |
| `redis-data` | Cache e dados de sessao do Redis |
| `api-logs` | Arquivos de log da aplicacao |
| `api-data` | Dados da aplicacao (chaves de protecao de dados, uploads) |
| `caddy-data` | Certificados SSL (se usando Caddy) |
| `caddy-config` | Estado de configuracao do Caddy (se usando Caddy) |

Para fazer backup de um volume manualmente:

```bash
docker run --rm -v mymascada_postgres-data:/data -v $(pwd):/backup \
  alpine tar czf /backup/postgres-data.tar.gz -C /data .
```

---

## Resolucao de Problemas

**"DB_PASSWORD is required"**
O arquivo `.env` esta ausente ou `DB_PASSWORD` nao esta definido. Execute `./setup.sh`
ou copie `.env.example` para `.env` e defina `DB_PASSWORD` com um valor seguro.

**Migracao falha na inicializacao**
Verifique os logs do container de migracao:
```bash
docker compose logs migration
```
Causas comuns: o banco de dados ainda nao esta pronto (geralmente resolve na proxima
tentativa) ou uma migracao anterior deixou o banco em estado inconsistente. Se o
container do postgres nao estiver saudavel, verifique seus logs com
`docker compose logs postgres`.

**Frontend nao consegue acessar a API**
Verifique se `CORS_ALLOWED_ORIGINS` no `.env` inclui sua `FRONTEND_URL`. Se estiver
usando um proxy reverso, certifique-se de que tanto a URL interna do Docker quanto a
URL publica externa estao incluidas. Por exemplo:
```
CORS_ALLOWED_ORIGINS=https://financas.exemplo.com,http://localhost:3000
```

**Email nao esta sendo enviado**
1. Confirme que `EMAIL_ENABLED=true` no `.env`.
2. Verifique se as credenciais SMTP estao corretas.
3. Verifique os logs do container da API para erros de email:
   ```bash
   docker compose logs api | grep -i email
   ```
4. Alguns provedores SMTP exigem senhas de aplicativo ou possuem limites de envio.

**Containers reiniciando constantemente**
Verifique os logs do servico com falha:
```bash
docker compose logs --tail=50 api
docker compose logs --tail=50 frontend
```

**Conflitos de porta**
Se as portas 3000 ou 5126 ja estiverem em uso no host, pare o servico conflitante
ou modifique os mapeamentos de porta no `docker-compose.yml`. Ao usar o proxy Caddy,
as portas 80 e 443 tambem devem estar disponiveis.

**Verificando a saude dos servicos**
```bash
docker compose ps
curl http://localhost:5126/health
```

---

## Visao Geral da Arquitetura

O stack Docker Compose executa os seguintes servicos:

```
                        +-------------------+
                        |   Caddy (proxy)   |  :80, :443
                        |   (opcional)      |
                        +--------+----------+
                                 |
                    +------------+------------+
                    |                         |
           +-------v-------+       +---------v---------+
           |    Frontend    |       |       API         |
           |   (Next.js)   |       |  (ASP.NET Core)   |
           |    :3000       |       |     :5126         |
           +---------------+       +----+----+---------+
                                        |    |
                              +---------+    +----------+
                              |                         |
                     +--------v--------+     +----------v---------+
                     |   PostgreSQL    |     |       Redis        |
                     |    :5432        |     |      :6379         |
                     +-----------------+     +--------------------+
```

Na primeira inicializacao, um container de **migracao** unico executa as migracoes
do EF Core no banco de dados e encerra. O container da API aguarda a conclusao da
migracao antes de iniciar.
