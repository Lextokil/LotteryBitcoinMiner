# Quick Start - Bitcoin Miner Console

## 🚀 Início Rápido

### 1. Configuração Inicial
Antes de executar, edite o arquivo `config/config.json` e configure seu endereço de carteira Bitcoin:

```json
{
  "pool": {
    "wallet": "SEU_ENDERECO_BITCOIN_AQUI"
  }
}
```

### 2. Executar o Minerador
```bash
cd BitcoinMinerConsole
dotnet run
```

### 3. Comandos Durante Execução
- **q** - Sair
- **s** - Estatísticas detalhadas
- **c** - Mostrar configuração
- **h** - Ajuda

## ⚠️ Importante

1. **Configure sua carteira Bitcoin** no arquivo `config/config.json`
2. Este é um **pool solo** - você só ganha se encontrar um bloco completo
3. A probabilidade é **extremamente baixa** com CPU
4. Projeto é **educacional** - demonstra como funciona a mineração Bitcoin

## 🔧 Compilação

```bash
# Compilar
dotnet build

# Executar
dotnet run

# Publicar (executável independente)
dotnet publish -c Release -r win-x64 --self-contained
```

## 📊 O que Esperar

- Hashrate típico: 500 KH/s - 2 MH/s (dependendo da CPU)
- Conexão com solo.ckpool.org:3333
- Interface console com estatísticas em tempo real
- Logs coloridos de atividade

## 🎯 Objetivo Educacional

Este projeto demonstra:
- Algoritmo SHA256
- Protocolo Stratum
- Estrutura de blocos Bitcoin
- Multi-threading em C#
- Comunicação TCP/IP

**Divirta-se aprendendo sobre Bitcoin!** 🚀
