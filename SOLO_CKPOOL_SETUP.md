# Solo.ckpool Setup Guide

Este guia explica as melhorias específicas implementadas para conectar com solo.ckpool.org usando o protocolo Stratum.

## Melhorias Implementadas

### 1. Autenticação Aprimorada
- **Username Format**: Agora suporta o formato `wallet.worker_name` para solo.ckpool
- **Fallback**: Se `worker_name` não estiver configurado, usa apenas o endereço da wallet

### 2. Gerenciamento de ExtraNonce
- **ExtraNonce1**: Extraído da resposta de subscription do pool
- **ExtraNonce2**: Gerado automaticamente com contador incremental
- **Size Management**: Tamanho do ExtraNonce2 configurado dinamicamente pelo pool

### 3. Processamento de Work Melhorado
- **Clean Jobs**: Detecta e processa corretamente requisições de clean jobs
- **Coinbase Transaction**: Construção adequada com ExtraNonce1 + ExtraNonce2
- **Merkle Root**: Cálculo correto da merkle root para solo mining

### 4. Submissão de Shares Otimizada
- **Format Compliance**: Submissões no formato correto para solo.ckpool
- **ExtraNonce2 Tracking**: Cada work item mantém seu próprio ExtraNonce2
- **Nonce Format**: Nonces formatados em hexadecimal lowercase

## Configuração

### config.json
```json
{
  "pool": {
    "url": "solo.ckpool.org",
    "port": 3333,
    "wallet": "bc1quuk6448yvxde48k7n96uc6d487pnycvdjf0p88",
    "worker_name": "miner01",
    "password": "x"
  },
  "mining": {
    "threads": 0,
    "intensity": "high",
    "target_temp": 80,
    "max_nonce": 4294967295
  },
  "logging": {
    "level": "info",
    "show_hashrate": true,
    "update_interval": 5,
    "log_to_file": false,
    "log_file": "mining.log"
  },
  "display": {
    "show_banner": true,
    "colored_output": true,
    "stats_refresh_rate": 2
  }
}
```

### Campos Importantes
- **wallet**: Seu endereço Bitcoin válido (obrigatório)
- **worker_name**: Nome do worker (opcional, mas recomendado)
- **url**: solo.ckpool.org (já configurado)
- **port**: 3333 (porta padrão do solo.ckpool)

## Eventos Stratum Suportados

### Client → Server
- `mining.subscribe`: Subscription com user agent
- `mining.authorize`: Autorização com wallet.worker format
- `mining.submit`: Submissão de shares com ExtraNonce2 correto

### Server → Client
- `mining.notify`: Recebimento de trabalho de mineração
- `mining.set_difficulty`: Ajuste de dificuldade
- `client.reconnect`: Requisições de reconexão
- `client.show_message`: Mensagens do pool

## Logs e Monitoramento

O minerador agora fornece logs detalhados específicos para solo.ckpool:

```
[INFO] Connecting to solo.ckpool.org:3333...
[NETWORK] Connected to pool
[NETWORK] Subscribing to mining...
[NETWORK] Subscription details: ExtraNonce1=01000000, ExtraNonce2Size=4
[NETWORK] Subscribed to mining
[NETWORK] Authorizing worker...
[NETWORK] Worker authorized
[MINING] New work received: job_12345
[NETWORK] New work received (Job: job_12345, Clean: true)
```

## Troubleshooting

### Problemas Comuns

1. **Authorization Failed**
   - Verifique se o endereço da wallet está correto
   - Confirme que é um endereço Bitcoin válido

2. **Connection Issues**
   - Verifique conectividade com solo.ckpool.org
   - Confirme que a porta 3333 não está bloqueada

3. **Share Rejections**
   - Verifique se o ExtraNonce2 está sendo gerado corretamente
   - Confirme que o nonce está no formato hexadecimal correto

### Debug Mode
Para debug detalhado, configure o nível de log para "debug" no config.json:

```json
"logging": {
  "level": "debug"
}
```

## Compatibilidade

Esta implementação é otimizada especificamente para:
- **solo.ckpool.org**: Pool de mineração solo Bitcoin
- **Stratum Protocol**: Versão 1 do protocolo Stratum
- **Bitcoin Network**: Mainnet Bitcoin

## Performance

### Recomendações
- Use múltiplas threads para melhor performance
- Configure `intensity` como "high" para máximo hashrate
- Monitor temperatura para evitar throttling

### Estatísticas
O minerador exibe estatísticas em tempo real:
- Hashrate atual e médio
- Shares aceitas/rejeitadas
- Dificuldade atual
- Status da conexão

## Segurança

### Boas Práticas
- Mantenha seu endereço de wallet privado
- Use conexões seguras quando possível
- Monitor logs para atividades suspeitas
- Backup regular da configuração

### Wallet Security
- Use apenas endereços Bitcoin que você controla
- Nunca compartilhe chaves privadas
- Considere usar hardware wallets para grandes quantias

## Suporte

Para problemas específicos do solo.ckpool:
- Verifique o status do pool em solo.ckpool.org
- Consulte a documentação oficial do pool
- Monitor logs do minerador para erros específicos

Para problemas do minerador:
- Verifique os logs detalhados
- Confirme configuração do config.json
- Teste conectividade de rede
