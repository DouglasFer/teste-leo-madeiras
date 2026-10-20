# Respostas

## Parte 1 — Questões conceituais

### 1. Síncrono x assíncrono
Em chamada Síncrona, o sistema realiza a requisição e fica esperando a resposta pra continuar, já na chamada assícrona o sistema envia a mensagem ou evento e não precisa esperar a resposta, ou seja, continua trabalhando, por exemplo, em chamadas síncronas é dificil lidar com picos de requisições, fazendo com que o servidor sobrecarregue levando a lentidão ou até ficar fora do ar e com isso nada vai ser executado, já com o assíncrono, mesmo estando fora do ar a api, a mensagem fica salva e faz o retry assim que a api voltar. Acredito que quase todas as chamadas são assíncronas do sistema pelo fato que precisa montar os planos e gerar arquivos(que demora e acaba consumindo uma boa parte do servidor dependendo do tamanho dos arquivos) também pelo fato do pagamento que depende do cliente, que as vezes não paga no mesmo momento

### 2. Mensagem duplicada
O pagamento pode ser processado duas vezes, fazendo com que duplique o pedido, um endpoint ser idempotente é quando existe a requisição mas ela não é feita nenhuma alteração no resultado final, por exemplo, enviei a mensagem do pagamento 1007 ela foi executada, porém chegou outra mensagem do mesmo pagamento, caso não tenha nenhuma alteração no pagamento, os dados que já foram salvos continuam igual, para garantir isso, posso validar o pagamento antes, conferindo o status no banco, validando se o pagamento já existe, também posso retornar um sucesso para evitar que o pagamento seja enviado novamente

### 3. Falha no meio do caminho
Acredito que o jeito mais simples é salvar o pedido e manter o status da mensagem Pendente e um registro avisando que o svl não recebeu/processou o pedido e com isso ter um processo em segundo plano lendo esses registros para tentar novamente, tendo um limite de retries

### 4. Códigos HTTP
201 - Created: pois ele informa que foi criado e retorna o resultado
400 - Bad Request: o servidor não entendeu a requisição por falta do serviceId(falta de algum dado no corpo da requisição)
401 - Unauthorized: falta autenticação(fazer o login para obter o token), a diferença para o 403(Forbidden) é que o 403 reflete mais a uma falta de permissão
404 - Not Found: não foi encontrado o dado solicitado no servidor
500 - Internal Server Error: erro genérico para tentar não expor informações do banco
    
### 5. Intermediário
A vantagem de ter a Digibee é o desacoplamento, ou seja, se tem varios sistemas chamando a Digibee, faço as alterações apenas nela, não precisando mudar todos os outros sistemas que chamam a Digibee, por exemplo, na troca da LeoPlan pelo CorteCloud sem a Digibee, eu precisaria alterar todos os sistemas que chamavam o LeoPlan, o que levaria mais tempo, tendo a Digibee, a alteração é só nela, além do monitoramento e tratamento de retries.
A desvantagem é que o tempo de processamento aumenta, e o tempo para manutenção também, já que vai ser um pouco mais dificil de rastrear onde é o problema

## Parte 3 — Leitura de código
### 3.1 Problemas encontrados

#### Problema 1: SQL concatenado (SQL injection)
Trecho:
var sql = "SELECT * FROM Pedidos WHERE PedidoSVL = '" + notif.PedidoSVL + "'";

errado: o Pedido vem direto do corpo da requisição e é concatenado na string do SQL, em vez de ser passado como parâmetro.
impacto: quem conseguir chamar o endpoint pode alterar a consulta e até deletar/alterar os dados da tabela. A correção é usar parâmetro (WHERE PedidoSVL = @PedidoSVL), que o próprio Dapper já suporta ou até mesmo Entity Framework que já faz essa parametrização das consultas.
Severidade: alta.

#### Problema 2: Digibee chamada só quando o pedido é novo
Trecho: _digibee.EnviaServicoParaProducaoAsync(...) está dentro do if (pedido == null).

errado: o método deveria mandar o serviço para produção sempre, mas, se o pedido já existe no banco, ele só atualiza o status para PAGO e não envia para a Digibee.
impacto: no fluxo normal, o pedido já existe quando o pagamento chega (ele foi criado na etapa do orçamento), então o else é o caminho mais comum, o cliente faz o pagamento, mas a mensagem não é enviada.
Severidade: alta.

#### Problema 3: falta de await na chamada da Digibee
Trecho: _digibee.EnviaServicoParaProducaoAsync(pedido.ServiceId, pedido.PedidoSVL);

errado: a chamada é assíncrona, mas está sem await, ou seja, é "disparada e esquecida".
impacto: o método termina sem esperar o envio se a chamada falhar, a exceção se perde e ninguém fica sabendo, não há log nem retries, pagamento fica registrado, mas o serviço pode não ter ido para produção.
Severidade: alta.

#### Problema 4: ServiceId nulo no pedido criado
Trecho: pedido = new Pedido { PedidoSVL = notif.PedidoSVL, Status = "PAGO" };

errado: na criação do objeto não existe o ServiceId, mas logo depois ele é enviado para a Digibee.
impacto: a Digibee recebe o ServiceId nulo, dependendo de quem recebe, a chamada dá erro ou é aceita com valor inválido, e o sistema não consegue identificar qual serviço colocar.
Severidade: alta.

#### Problema 5: status CONCLUIDO gravado antes da chamada ao SVL
Trecho: controle.Status = "CONCLUIDO"; await _svl.NotificarPagamentoAsync(pedido.PedidoSVL);

errado: o status é alterado para CONCLUIDO antes de chamar o SVL. Se a chamada falhar, o objeto continua com CONCLUIDO e é gravado assim no UpdateAsync do final.
impacto: o banco diz que o SVL foi avisado, mas ele não foi. Como o worker só tenta de novo as notificações PENDENTE, esse aviso nunca mais é reenviado.
Severidade: alta.

#### Problema 6: catch vazio
Trecho: catch (Exception)
{
}

errado: o erro não é atribuído a nada, não muda o status para PENDENTE, não incrementa as tentativas e não registra log.
impacto: se ocorrer algum erro, não há como saber, nem o worker nem a equipe ficam sabendo que o SVL não foi avisado.
Severidade: alta.

#### Problema 7: pedido e controle gravados sem transação
Trecho: o InsertAsync(pedido)/UpdateAsync(pedido) e o InsertAsync(controle) são gravações separadas.
errado: se a aplicação cair entre as duas gravações (deploy, reinício do servidor), o pedido fica salvo como PAGO, mas sem nenhum registro de controle.
impacto: sem o registro de controle, o worker não tem o que reprocessar e o SVL nunca é avisado. A correção é gravar o pedido e o controle na mesma transação: ou salva os dois, ou nenhum.
Severidade: média

#### Problema 8: sem idempotência
Trecho: o método inteiro; não há nenhuma verificação se o pagamento já foi processado.
errado: se o ERP enviar a mesma notificação duas vezes, tudo é executado de novo.
impacto: é criado um segundo ControleNotificacao e o SVL é avisado duas vezes, o serviço também iria para produção duas vezes.
Severidade: alta.

### 3.2 Método corrigido
    public async Task ProcessarPagamentoAsync(NotificacaoPagamento notif){
        using var transacao = _db.BeginTransaction();

        var pedido = await _db.QueryFirstOrDefaultAsync<Pedido>(
        "SELECT * FROM Pedidos WHERE PedidoSVL = @PedidoSVL",
        new { notif.PedidoSVL });

        if(pedido != null && pedido.Status != "CRIADO"){
            _logger.LogInformation($"Pagamento do pedido {notif.PedidoSVL} já processado.")
            return;
        }

        if(pedido == null){
            pedido = new Pedido { PedidoSVL = notif.PedidoSVL, Status = "PAGO", ServiceId = notif.ServiceId};
            await _db.InsertAsync(pedido, transacao);
        }else{
            pedido.Status = "PAGO";
            await _db.UpdateAsync(pedido, transacao);
        }

        var controle = new  ControleNotificacao { PedidoSVL = pedido.PedidoSVL, Status = "INICIAL" };
        await _db.InsertAsync(controle, transacao);
        await _digibee.EnviaServicoParaProducaoAsync(pedido.ServiceId, pedido.PedidoSVL);

        transacao.Commit();

        try{            
            await _svl.NotificarPagamentoAsync(pedido.PedidoSVL);
            controle.Status = "CONCLUIDO";
        }
        catch(Exception ex){    
            controle.Status = "PENDENTE";
            controle.Tentativas++;
            _logger.LogError($"Erro ao processar o pedido: {pedido} ele ficará PENDENTE, message: {ex.message}")
        }

        await _db.UpdateAsync(controle);
    }

### 3.3 Pagamento Duplicado
No código original, na segunda notificação o pedido já existe, então o código cai no else, atualiza o status para PAGO de novo (mesmo sem mudança), cria outro ControleNotificacao e avisa o SVL pela segunda vez se a Digibee fosse chamada sempre, o serviço seria enviado duas vezes.
No código corrigido, isso fica resolvido pela checagem no início do método se o pedido já existe com status diferente de CRIADO, o pagamento já foi processado, então o método só registra no log e retorna, sem gravar nada, sem criar outro controle e sem chamar a Digibee ou o SVL. Quem chamou recebe sucesso e não reenvia.

## Parte 4 — SQL e modelagem

### SQL 4.1
select TipoPedido, Count(*) from Pedido where DataCriacao >= '2026-09-01' and DataCriacao < '2026-10-01' group by TipoPedido

### SQL 4.2
select * from Pedido p where p.Status = 'PAGO' and p.DataCriacao < NOW() - INTERVAL '2 hours' and not exists(
select 1 from ControleNotificacao c where c.PedidoId = p.Id and c.Status = 'CONCLUIDO')

### SQL 4.3
select p.LojaId, Count(*) from ControleNotificacao c join Pedido p on p.Id = c.PedidoId where c.Status = 'FALHA' group by p.LojaId Order By count(*) Desc limit 5

### Questão 4.4
Sim é um problema pois o pedido pode ser executado duas vezes, a alteração mais simples é transformar a coluna PedidoSVL em unique, por que assim vai evitar que PedidosSVL com o mesmo valor seja salvo no banco, se caso a tabela já existir algum duplicado, precisa ser tratado antes de alterar a coluna para unique, se não, da erro

### Questão 4.5
Na tabela de Pedido criar os  indices composto de Status e DataCriacao e na tabela de ControleNotificacao criaria o indices composto do PedidoId e Status