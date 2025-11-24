# 🔧 Guia de Solução de Problemas - MQTT Gateway

## Problemas Comuns e Soluções

### 🚫 Problemas de Conexão

#### ❌ "Conexão com MQTT falhou"
**Sintomas:** API não consegue conectar com o broker MQTT

**Possíveis Causas:**
- Broker MQTT não está rodando
- Configuração incorreta de conexão
- Firewall bloqueando a porta 1883

**Soluções:**
1. Verificar se o Mosquitto está rodando:
   ```bash
   docker ps | grep mosquitto
   ```

2. Testar conexão manualmente:
   ```bash
   # Instalar cliente MQTT
   winget install mosquitto
   
   # Testar publicação
   mosquitto_pub -h localhost -p 1883 -t test/topic -m "hello world"
   
   # Testar subscrição (em outro terminal)
   mosquitto_sub -h localhost -p 1883 -t test/topic
   ```

3. Verificar configuração em `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "MqttBroker": "Server=localhost;Port=1883;CleanSession=true"
     }
   }
   ```

#### ❌ "SignalR connection failed"
**Sintomas:** Cliente não consegue conectar via WebSocket

**Soluções:**
1. Verificar se a API está rodando:
   ```bash
   curl https://localhost:8081/swagger
   ```

2. Verificar CORS se rodando de origem diferente
3. Confirmar que o sessionId é um GUID válido
4. Verificar certificados SSL em desenvolvimento

### 🔄 Problemas de Mensagens

#### ❌ "Mensagens não chegam aos clientes"
**Sintomas:** Mensagens enviadas via API não aparecem no SignalR

**Diagnóstico:**
1. Verificar logs da aplicação
2. Confirmar que a sessão está ativa
3. Verificar se o tópico MQTT está correto

**Soluções:**
1. Verificar se há clientes conectados para a sessão:
   ```csharp
   // No código, adicionar logs em SessionManagerService
   ```

2. Testar fluxo completo via Swagger + Cliente HTML

#### ❌ "Contexto não é carregado para novos clientes"
**Sintomas:** Novos clientes não recebem histórico da sessão

**Soluções:**
1. Verificar se `SessionContextStore` está funcionando
2. Confirmar que `CreateContext` é chamado corretamente
3. Verificar logs de inicialização da sessão

### 🐳 Problemas com Docker

#### ❌ "docker-compose build failed"
**Soluções:**
1. Limpar cache do Docker:
   ```bash
   docker system prune -a
   ```

2. Verificar se o Dockerfile está correto
3. Verificar conexão com internet para download de imagens

#### ❌ "Permission denied" no Windows
**Soluções:**
1. Executar PowerShell como administrador
2. Configurar compartilhamento de drives no Docker Desktop
3. Verificar permissões nas pastas do projeto

### 📊 Problemas de Performance

#### ⚠️ "Muitas conexões/mensagens lentas"
**Sintomas:** API fica lenta com muitas sessões

**Otimizações:**
1. Implementar limpeza periódica de sessões inativas
2. Considerar usar Redis para `SessionContextStore` em produção
3. Implementar rate limiting

### 🔍 Debugging e Logs

#### Habilitar logs detalhados
No `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore.SignalR": "Debug",
      "MqttGateway.Server": "Debug",
      "MQTTnet": "Debug"
    }
  }
}
```

#### Logs importantes para monitorar
- Conexões/desconexões SignalR
- Assinaturas/desassinaturas MQTT
- Criação/remoção de contextos de sessão
- Erros de parsing de mensagens MQTT

### 🧪 Teste Manual Completo

#### 1. Testar MQTT diretamente
```bash
# Terminal 1 - Subscrever
mosquitto_sub -h localhost -p 1883 -t "personal/+/550e8400-e29b-41d4-a716-446655440000/+"

# Terminal 2 - Publicar
mosquitto_pub -h localhost -p 1883 -t "personal/test-client/550e8400-e29b-41d4-a716-446655440000/test" -m "Hello World"
```

#### 2. Testar API REST
```bash
curl -X POST "https://localhost:8081/Messages/Send" \
     -H "Content-Type: application/json" \
     -d '{
       "sessionId": "550e8400-e29b-41d4-a716-446655440000",
       "message": "Test message",
       "channel": "test"
     }'
```

#### 3. Testar Cliente SignalR
Usar o arquivo `Examples/Client/index.html` para teste completo.

### 📞 Quando Pedir Ajuda

Se o problema persistir, forneça:

1. **Logs completos** da aplicação
2. **Versão** do .NET e Docker
3. **Sistema operacional** e versão
4. **Passos exatos** para reproduzir o problema
5. **Configuração** sanitizada (sem senhas)
6. **Comportamento esperado** vs comportamento atual

### 🛠️ Ferramentas Úteis

- **MQTT Explorer**: GUI para explorar tópicos MQTT
- **Postman**: Testar APIs REST
- **Browser DevTools**: Debug de conexões SignalR
- **Docker Desktop**: Monitorar containers
- **Visual Studio/VS Code**: Debug da aplicação
- **Docker Compose**: Gerenciar serviços Docker
- **Docker CLI**: Comandos básicos de Docker
- **Docker Desktop**: Monitorar containers

### 📚 Recursos Adicionais

- [Documentação MQTTnet](https://github.com/dotnet/MQTTnet)
- [SignalR Troubleshooting](https://docs.microsoft.com/en-us/aspnet/core/signalr/troubleshoot)
- [Mosquitto Documentation](https://mosquitto.org/documentation/)
- [Docker Troubleshooting](https://docs.docker.com/desktop/troubleshoot/)