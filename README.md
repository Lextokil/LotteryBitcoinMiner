# Bitcoin Miner Console

Um minerador de Bitcoin real em C# que se conecta ao pool solo.ckpool.org para mineração de Bitcoin. Este projeto foi desenvolvido para fins educacionais e demonstra como funciona a mineração de Bitcoin, incluindo implementação SHA256, protocolo Stratum e comunicação com pools de mineração.

## 🚀 Características

- **Mineração Real**: Conecta-se ao solo.ckpool.org para mineração real de Bitcoin
- **Multi-threading**: Utiliza todos os cores da CPU para máxima performance
- **SHA256 Otimizado**: Implementação SHA256 do zero com otimizações
- **Protocolo Stratum**: Implementação completa do protocolo Stratum
- **Interface Console**: Interface rica em console com estatísticas em tempo real
- **Configuração Flexível**: Sistema de configuração via JSON
- **Logs Detalhados**: Sistema de logging com cores e níveis
- **Comandos Interativos**: Comandos durante execução para controle

## 📋 Pré-requisitos

- .NET 8.0 SDK ou superior
- Windows 10/11 (testado)
- Conexão com internet
- Endereço de carteira Bitcoin (para receber recompensas)

## 🛠️ Instalação

1. Clone ou baixe o projeto
2. Navegue até o diretório do projeto:
   ```bash
   cd BitcoinMinerConsole
   ```

3. Restaure as dependências:
   ```bash
   dotnet restore
   ```

4. Compile o projeto:
   ```bash
   dotnet build
   ```

## ⚙️ Configuração

Antes de executar, configure seu endereço de carteira Bitcoin no arquivo `config/config.json`:

```json
{
  "pool": {
    "url": "solo.ckpool.org",
    "port": 3333,
    "wallet": "SEU_ENDERECO_BITCOIN_AQUI",
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

### Configurações Importantes:

- **wallet**: Seu endereço de carteira Bitcoin (obrigatório para receber recompensas)
- **threads**: Número de threads (0 = auto-detectar cores da CPU)
- **intensity**: Intensidade da mineração ("low", "medium", "high")
- **worker_name**: Nome do worker (identificação no pool)

## 🚀 Execução

Execute o minerador:

```bash
dotnet run
```

Ou execute o binário compilado:

```bash
dotnet BitcoinMinerConsole.dll
```

## 🎮 Comandos Durante Execução

Durante a execução, você pode usar os seguintes comandos:

- **q** - Sair do minerador
- **s** - Mostrar estatísticas detalhadas
- **c** - Mostrar configuração atual
- **r** - Reiniciar conexão com pool
- **h** - Mostrar ajuda

## 📊 Interface

O minerador exibe uma interface rica em console com:

```
═══════════════════════════════════════════════════════════════
                    BITCOIN MINER CONSOLE                     
═══════════════════════════════════════════════════════════════
Pool: solo.ckpool.org:3333
Status: Mining ✓
Worker: miner01
───────────────────────────────────────────────────────────────
Current Job: 12345abc
Difficulty: 73,197,634,206,448
Threads: 8/8
───────────────────────────────────────────────────────────────
Hashrate: 1.2 MH/s (avg: 1.1 MH/s)
Total Hashes: 1,234,567,890
Shares: 15 accepted, 0 rejected
Uptime: 0d 02h 15m 33s
═══════════════════════════════════════════════════════════════
Commands: [q]uit, [s]tats, [c]onfig, [h]elp
```

## 🏗️ Arquitetura

O projeto está organizado em módulos:

### Core
- **SHA256Hasher**: Implementação SHA256 otimizada
- **WorkItem**: Estrutura de trabalho de mineração
- **MiningEngine**: Motor principal de mineração multi-threaded

### Network
- **StratumClient**: Cliente do protocolo Stratum
- **StratumMessage**: Mensagens do protocolo Stratum

### Configuration
- **MinerConfig**: Classes de configuração
- **ConfigLoader**: Carregador de configuração

### Logging
- **ConsoleLogger**: Sistema de logging colorido
- **StatsDisplay**: Display de estatísticas em tempo real

## 🔧 Desenvolvimento

### Estrutura do Projeto

```
BitcoinMinerConsole/
├── src/
│   ├── Core/                 # Motor de mineração
│   ├── Network/              # Comunicação Stratum
│   ├── Configuration/        # Sistema de configuração
│   ├── Logging/              # Sistema de logging
│   └── Program.cs            # Programa principal
├── config/
│   └── config.json           # Configuração
└── README.md
```

### Compilação para Release

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## ⚠️ Avisos Importantes

1. **Mineração Solo**: Este minerador conecta-se ao solo.ckpool.org, que é um pool solo. Isso significa que você só receberá recompensas se encontrar um bloco completo.

2. **Probabilidade**: A probabilidade de encontrar um bloco com CPU é extremamente baixa. Este projeto é principalmente educacional.

3. **Consumo de Energia**: A mineração consome muita energia da CPU. Monitor a temperatura do seu sistema.

4. **Carteira Bitcoin**: Certifique-se de usar um endereço de carteira Bitcoin válido e que você controla.

## 📈 Performance

Performance típica em diferentes CPUs:

- **Intel i5-8400**: ~800 KH/s
- **Intel i7-9700K**: ~1.2 MH/s
- **AMD Ryzen 5 3600**: ~1.5 MH/s
- **AMD Ryzen 7 3700X**: ~2.1 MH/s

*Nota: Estes valores são aproximados e podem variar.*

## 🐛 Solução de Problemas

### Erro de Conexão
- Verifique sua conexão com internet
- Confirme se o firewall não está bloqueando a porta 3333

### Autorização Falhada
- Verifique se o endereço da carteira Bitcoin está correto
- Certifique-se de que o endereço é válido

### Performance Baixa
- Ajuste o número de threads na configuração
- Verifique se outros programas não estão consumindo CPU
- Considere ajustar a intensidade para "medium" ou "low"

## 📚 Recursos Educacionais

Este projeto demonstra:

- Como funciona o algoritmo SHA256
- Estrutura de blocos Bitcoin
- Protocolo Stratum de mineração
- Comunicação TCP/IP
- Multi-threading em C#
- Arquitetura de software modular

## 🤝 Contribuição

Contribuições são bem-vindas! Por favor:

1. Fork o projeto
2. Crie uma branch para sua feature
3. Commit suas mudanças
4. Push para a branch
5. Abra um Pull Request

## 📄 Licença

Este projeto é para fins educacionais. Use por sua própria conta e risco.

## 🔗 Links Úteis

- [Solo CK Pool](https://solo.ckpool.org/)
- [Bitcoin Wiki - Mining](https://en.bitcoin.it/wiki/Mining)
- [Stratum Protocol](https://en.bitcoin.it/wiki/Stratum_mining_protocol)
- [SHA256 Algorithm](https://en.wikipedia.org/wiki/SHA-2)

## 📞 Suporte

Para dúvidas ou problemas, abra uma issue no repositório do projeto.

---

**Disclaimer**: Este software é fornecido "como está" sem garantias. A mineração de Bitcoin consome energia e pode não ser lucrativa. Use por sua própria conta e risco.
