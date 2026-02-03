**Idioma / Language:** [English](README.md) | Portugues (BR)

# MyMascada

Uma aplicacao de gestao financeira pessoal com categorizacao de transacoes por IA, sincronizacao bancaria e suporte multilinguagem. Desenvolvido para self-hosting com Docker, o MyMascada te da controle total sobre seus dados financeiros.

## Funcionalidades

- **Gestao de Transacoes** -- Importe transacoes via CSV, OFX ou entrada manual
- **Categorizacao por IA** _(opcional -- requer chave da API OpenAI)_ -- Categorizacao automatica de transacoes usando GPT-4o-mini
- **Categorizacao por Regras** -- Defina regras personalizadas para categorizar transacoes automaticamente
- **Sincronizacao Bancaria** _(opcional -- apenas Nova Zelandia)_ -- Sincronizacao automatica de contas via Akahu
- **Login com Google** _(opcional)_ -- Autenticacao por email e senha sempre disponivel
- **Notificacoes por Email** _(opcional)_ -- Recuperacao de senha, verificacao de email e alertas
- **Suporte Multilinguagem** -- Ingles e Portugues Brasileiro
- **Suporte PWA** -- Instalavel em dispositivos moveis para experiencia nativa
- **Controle de Orcamento** -- Defina orcamentos e analise padroes de gastos
- **Reconciliacao de Contas** -- Compare e reconcilie saldos de contas
- **Exportacao de Dados** -- Exporte seus dados em formato CSV ou JSON
- **Self-Hosting via Docker** -- Configuracao guiada com um unico script

## Inicio Rapido (Self-Hosting)

```bash
git clone https://github.com/digaomatias/mymascada.git
cd mymascada
./setup.sh
```

O script de configuracao te guia pelo processo, incluindo credenciais do banco de dados, chaves de API opcionais e configuracoes de email. Para instrucoes detalhadas, referencia de variaveis de ambiente e orientacoes para deploy em producao, consulte o [SELF-HOSTING.pt-BR.md](SELF-HOSTING.pt-BR.md).

## Arquitetura

O MyMascada segue os principios de Clean Architecture com camadas claramente separadas:

```
Domain  -->  Application  -->  Infrastructure  -->  WebAPI
```

- **Domain** -- Entidades, value objects e eventos de dominio sem dependencias externas
- **Application** -- Logica de negocio, casos de uso, CQRS com MediatR e DTOs
- **Infrastructure** -- Acesso a dados (EF Core), integracoes externas (OpenAI, email, Akahu)
- **WebAPI** -- Endpoints REST, autenticacao, middleware e injecao de dependencia

O frontend e uma aplicacao Next.js independente que se comunica com o backend via APIs REST.

## Stack Tecnologica

| Componente | Tecnologia |
|-----------|-----------|
| Backend | ASP.NET Core 10, C# |
| Frontend | Next.js 15, React 19, TypeScript |
| Estilizacao | Tailwind CSS |
| Banco de Dados | PostgreSQL 16 |
| ORM | Entity Framework Core 10 |
| Cache | Redis 7 |
| Autenticacao | JWT, Google OAuth |
| IA | OpenAI API (gpt-4o-mini) |
| Internacionalizacao | next-intl (frontend), IStringLocalizer (backend) |
| Jobs em Background | Hangfire |
| Testes | xUnit, Playwright |

## Configuracao para Desenvolvimento

### Pre-requisitos

- .NET 10 SDK
- Node.js 20+
- PostgreSQL 16+
- Redis 7+ (opcional para desenvolvimento)

### Backend

```bash
cd src/WebAPI/MyMascada.WebAPI
dotnet restore
dotnet run
```

A API inicia em `https://localhost:5126` por padrao.

### Frontend

```bash
cd frontend
npm install
npm run dev
```

O frontend inicia em `http://localhost:3000` por padrao.

### Executando Testes

```bash
# Testes unitarios do backend
dotnet test

# Testes E2E do frontend
cd frontend
npx playwright test
```

## Documentacao

- [Guia de Self-Hosting](SELF-HOSTING.pt-BR.md) -- Implantacao, configuracao e deploy em producao
- [Contribuindo](CONTRIBUTING.md) -- Como contribuir com o projeto
- [Politica de Seguranca](SECURITY.md) -- Como reportar vulnerabilidades
- [Privacidade](PRIVACY.md) -- Tratamento de dados e informacoes de privacidade
- [Changelog](CHANGELOG.md) -- Historico de versoes e mudancas
- [Codigo de Conduta](CODE_OF_CONDUCT.md) -- Diretrizes da comunidade

## Licenca

Este projeto e licenciado sob a [GNU Affero General Public License v3.0](LICENSE) (AGPL-3.0).

## Contribuindo

O MyMascada e atualmente um projeto pessoal, mas contribuicoes sao bem-vindas. Se voce encontrar um bug ou tiver uma sugestao de funcionalidade, por favor [abra uma issue](https://github.com/digaomatias/mymascada/issues). Pull requests sao apreciados -- apenas descreva a mudanca e inclua testes relevantes.
