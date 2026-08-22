# GamePrice.Api

API REST do ecossistema GamePrice. Ela concentra a autenticação dos usuários, a persistência de dados, a lista de desejos e a comunicação com o serviço de scraping responsável pelas informações de preços, ofertas e jogos gratuitos.

## Funcionalidades

- Autenticação com JWT e suporte a cookie `HttpOnly`.
- Cadastro, login, logout, atualização de perfil e alteração de senha.
- Lista de desejos autenticada, com definição de preço-alvo.
- Catálogo e histórico de preços persistidos em SQLite.
- Endpoints para pesquisa de jogos, comparação de preços, ofertas e jogos gratuitos.
- Cache em memória, cache de respostas e atualização periódica das ofertas.
- Documentação interativa com Swagger em ambiente de desenvolvimento.
- Logs estruturados com Serilog e endpoint de integridade.

## Arquitetura

```text
GamePrice (MVC) -> GamePrice.Api -> GamePrice.Scraper -> Lojas e feeds
                         |
                         -> SQLite
```

A API recebe as requisições da interface web. Consultas de preços e de catálogo são encaminhadas ao scraper por HTTP. Dados de usuários, lista de desejos, catálogo e histórico são armazenados localmente no SQLite.

## Tecnologias

- .NET 9 e ASP.NET Core Web API
- Entity Framework Core e SQLite
- JWT Bearer Authentication
- Serilog
- Swagger / OpenAPI
- Docker

## Pré-requisitos

- .NET SDK 9.0 ou superior compatível
- `GamePrice.Scraper` em execução, por padrão em `http://localhost:8000`

## Execução local

No diretório deste projeto, execute:

```powershell
dotnet restore
dotnet run --launch-profile http
```

A API será iniciada em `http://localhost:5098`. Em ambiente de desenvolvimento, a documentação Swagger estará em `http://localhost:5098/swagger`.

Na primeira inicialização, o banco de dados SQLite é criado em `Data/gameprice.db` e recebe os dados de catálogo necessários.

## Configuração

As configurações principais estão em `appsettings.json`:

| Chave | Finalidade |
| --- | --- |
| `ConnectionStrings:GamePrice` | Caminho e opções do banco SQLite. |
| `ApiSettings:ScraperApiUrl` | URL do serviço de scraping. |
| `Jwt` | Emissor, público, duração e chave do token. |
| `Cache` | Tempo de expiração do cache de dados. |
| `Feeds` | Frequência de atualização das ofertas. |
| `DatabaseMaintenance` | Política de limpeza de dados antigos. |

Para produção, substitua a chave JWT de desenvolvimento por uma chave forte mantida fora do repositório, por exemplo através de variáveis de ambiente ou User Secrets.

## Principais endpoints

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/api/health` | Estado da API e do banco de dados. |
| `POST` | `/api/auth/register` | Cria uma conta. |
| `POST` | `/api/auth/login` | Autentica e retorna um token JWT. |
| `POST` | `/api/auth/logout` | Encerra a sessão. |
| `GET` | `/api/scraper/price?gameName={nome}` | Retorna um preço encontrado. |
| `GET` | `/api/scraper/prices?gameName={nome}` | Retorna preços encontrados em múltiplas lojas. |
| `GET` | `/api/scraper/search?query={termo}&limit=8` | Retorna sugestões de pesquisa. |
| `GET` | `/api/scraper/deals` | Retorna as ofertas em destaque. |
| `GET` | `/api/scraper/free-games` | Retorna jogos gratuitos e promoções ativas. |
| `GET` | `/api/profile` | Retorna o perfil autenticado. |
| `GET` | `/api/wishlist` | Retorna a lista de desejos autenticada. |

As rotas de perfil e lista de desejos exigem o cabeçalho `Authorization: Bearer {token}` ou o cookie de autenticação emitido no login.

## Execução com Docker

O ambiente completo é orquestrado pelo arquivo `docker-compose.yml` do repositório [GamePrice](https://github.com/Gabriel-Oliveira-Prado/GamePrice). Com os três repositórios clonados em diretórios irmãos, execute no diretório do projeto web:

```powershell
docker compose up --build
```

Nesse modo, a API fica disponível em `http://localhost:5200` e armazena o SQLite no volume persistente `gameprice-data`.

## Projetos relacionados

- [GamePrice](https://github.com/Gabriel-Oliveira-Prado/GamePrice): aplicação web MVC.
- [GamePrice.Scraper](https://github.com/Gabriel-Oliveira-Prado/GamePrice.Scraper): serviço de coleta de dados.

## Autor

Desenvolvido por [Gabriel Oliveira Prado](https://github.com/Gabriel-Oliveira-Prado).
