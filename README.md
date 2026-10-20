# API Foundation — Arquivos de Produção

Teste técnico (Desenvolvedor Júnior · Integrações).

Web API em .NET 8 que simula a API Foundation recebendo da Digibee os links dos arquivos de produção gerados pelo CorteCloud. Os arquivos de pedidos ZLPL/ZCVS são copiados para o armazenamento interno e os pedidos ZMOB notificam a LeomobAPI, com controle de status e reprocessamento.

## Stack

- C# / .NET 8 (ASP.NET Core Web API com controllers)
- Entity Framework Core 8 + PostgreSQL 16 (Npgsql)
- xUnit + SQLite em memória nos testes
- Docker Compose
- Swagger (Swashbuckle)

## Como rodar

### Opção 1: Docker
Pré-requisito: Docker Desktop aberto.

bash
docker compose up --build


Sobe o PostgreSQL e a API. Na inicialização a API aplica as migrations e cria os pedidos do seed (1007, 1008 e 1012).

- Swagger: http://localhost:5000/swagger
- API: http://localhost:5000

Para parar: Ctrl+C. Para parar e apagar os dados do banco:

bash
docker compose down -v


### Opção 2: dotnet run

Pré-requisitos: .NET 8 SDK e um PostgreSQL em localhost:5432 com usuário e senha postgres (connection string em src/ApiFoundation/appsettings.json).

bash
dotnet run --project src/ApiFoundation


O endereço aparece no console (Now listening on: http://localhost:5258); o Swagger fica em /swagger.

### Testes

bash
dotnet test


Não precisa de Docker nem de banco: os testes usam SQLite em memória.

## Endpoints

POST /api/arquivos-producao - Recebe os links de produção enviados pela Digibee
POST /api/notificacoes/reprocessar - Simula o worker: tenta de novo as notificações PENDENTE
GET  /api/pedidos/{pedidoSVL} - Retorna o pedido, seus links e o status da notificação

### Respostas do POST /api/arquivos-producao

Envio processado (mesmo que algum serviço tenha falhado) - 200, com o resultado de cada serviço
hasErrors = true - 422, envio inteiro rejeitado
JSON inválido ou sem campo obrigatório (ex.: services) - 400 (validação automática do [ApiController])

Exemplo de resposta com um pedido inexistente:

json
{
  "resultados": [
    { "serviceId": 21763653, "pedidoSVL": 1007, "sucesso": true,  "erro": null },
    { "serviceId": 1,        "pedidoSVL": 9999, "sucesso": false, "erro": "Pedido não encontrado" }
  ]
}


## Estrutura


src/ApiFoundation/
  Controllers/   HTTP: recebe a requisição e escolhe o status de resposta
  Services/      Regras de negócio (processamento, notificação, consulta)
  Integracoes/   Contratos com sistemas externos e suas implementações falsas
  Models/        Entidades do banco
  Data/          AppDbContext, configuração do modelo, seed e migrations
  Dtos/          Formato do JSON de entrada e saída
tests/ApiFoundation.Tests/
  Os 5 cenários obrigatórios


## Testes automatizados

Pedido ZMOB não aciona o armazenamento de arquivos - PedidoZmob_NaoAcionaArmazenamento
Pedido ZLPL aciona o armazenamento uma vez por arquivo -  PedidoZlpl_AcionaArmazenamentoUmaVezPorArquivo
Envio repetido não duplica links nem notificações - EnvioRepetido_NaoDuplicaLinksCopiasNemNotificacoes
Notificação vai para FALHA após a terceira tentativa sem sucesso - Notificacao_VaiParaFalhaAposTerceiraTentativaSemSucesso
Envio com um pedido inexistente processa os demais normalmente - EnvioComPedidoInexistente_ProcessaOsDemaisNormalmente

## Premissas e decisões

Banco e infraestrutura

- PostgreSQL com migrations aplicadas na inicialização (Database.Migrate()); o seed é feito com HasData e os dados persistem entre execuções.
- Nos testes uso SQLite em memória: os testes validam regras de negócio, o código só usa LINQ e os índices únicos funcionam igual. Assim o dotnet test roda sem dependências externas.
- A connection string com senha fixa serve apenas para desenvolvimento local. Em produção ela viria de variável de ambiente ou cofre de segredos.
- O Docker Compose roda a API em Development para expor o Swagger, por ser um ambiente de avaliação. O PostgreSQL do compose não é exposto na máquina (a API acessa pela rede interna), para não conflitar com um Postgres local na porta 5432.

Recebimento

- hasErrors = true => 422 Unprocessable Entity: o JSON é válido, mas a própria Digibee sinalizou erro no conteúdo, então nenhum serviço é processado.
- Envio com alguns serviços com erro → 200, com o resultado de cada serviço no corpo (regra 2: um serviço com erro não derruba os demais).
- Pedido inexistente e serviceId divergente são erros daquele serviço e são registrados em log como Warning.
- As categorias de links (corte.pdfSumario, corte.pdfDetalhado, etiquetas, usinagem) são opcionais: um serviço pode vir sem alguma delas.
- Todos os links ficam numa única tabela, com uma coluna Categoria.

Idempotência

- Um link é identificado por (pedido, url), com índice único no banco. Antes de gravar, o serviço carrega as URLs já salvas num HashSet, o que também trata URLs repetidas dentro do mesmo envio.
- Só os links novos são copiados (ZLPL/ZCVS) ou disparam notificação (ZMOB). Um reenvio idêntico não gera links, cópias nem notificações.
- A cópia dos arquivos acontece antes do SaveChanges: se a cópia falhar, os links não são gravados e um reenvio da Digibee tenta de novo.

Notificação à LeomobAPI (ZMOB)

- Uma notificação por pedido (índice único em PedidoId). Se um ZMOB receber links novos depois de já ter sido notificado, não é notificado de novo; a LeomobAPI pode consultar GET /api/pedidos/{pedidoSVL}.
- O registro é gravado com status INICIAL antes da chamada. Sucesso → CONCLUIDO. Falha => PENDENTE e Tentativas + 1.
- Tentativas conta as tentativas sem sucesso, incluindo a primeira, feita no recebimento. Na 3ª falha a notificação vai para FALHA e não é mais tentada.
- Uma falha da LeomobAPI não marca o serviço como erro na resposta: os links foram salvos e a notificação fica pendente para o reprocessamento.
- A implementação falsa da LeomobAPI falha de forma aleatória, numa taxa configurável em appsettings.json (Leomob:TaxaFalha, de 0 = nunca falha a 1 = sempre falha). Nos testes a taxa é fixada em 0 ou 1 para o resultado ser previsível.

Outros

- Status e tipos de pedido são enum, gravados como texto no banco.

## O que eu faria com mais tempo

- Concorrência: dois envios idênticos ao mesmo tempo podem passar pela checagem e um deles falhar no índice único. Eu trataria esse erro (DbUpdateException) como duplicata e responderia sucesso.
- Isolar a falha de cada serviço: hoje, se o banco falhar no meio de um envio, a requisição inteira falha. Com um try/catch por serviço, só aquele serviço viraria erro na resposta.
- Mensageria com rabbitMQ: hoje o endpoint processa tudo dentro da requisição, e as notificações pendentes dependem de alguem chamar o /reprocessar, com uma fila a API so validaria o envio, publicaria uma mensagem e responderia com sucesso na hora que foi recebida a mensagem, o consumidor processaria os serviços em segundo plano.
- Clean Architeture: hoje o projeto é uma unica WebApi organizada em pastas, com o crescimento eu separaria os projetos com dependências em uma só direção: Domain, Application, Infraestructure, e API.
- DDD(entidades com comportamento): as entidades do projeto só guardam os dados e as regras ficam nos serviços, eu levaria as regras para dentro do domínio e teria métodos para que as regras ficam num lugar só