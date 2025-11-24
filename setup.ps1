# MQTT Gateway - Setup Script
# ==============================

Write-Host "MQTT Gateway - Script de Configuração" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""

# Verificar se o Docker está instalado e rodando
Write-Host "📋 Verificando pré-requisitos..." -ForegroundColor Yellow

try {
    $dockerVersion = docker --version
    Write-Host "✅ Docker encontrado: $dockerVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker não encontrado. Por favor, instale o Docker Desktop." -ForegroundColor Red
    exit 1
}

try {
    $dockerComposeVersion = docker-compose --version
    Write-Host "✅ Docker Compose encontrado: $dockerComposeVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker Compose não encontrado. Por favor, instale o Docker Compose." -ForegroundColor Red
    exit 1
}

# Verificar se o .NET 8 está instalado
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET encontrado: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET 8 SDK não encontrado. Por favor, instale o .NET 8 SDK." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🔧 Configurando ambiente..." -ForegroundColor Yellow

# Navegar para o diretório da solução
$solutionPath = Join-Path $PSScriptRoot "Solution"
if (Test-Path $solutionPath) {
    Set-Location $solutionPath
    Write-Host "📁 Diretório da solução: $solutionPath" -ForegroundColor Green
} else {
    Write-Host "❌ Diretório da solução não encontrado: $solutionPath" -ForegroundColor Red
    exit 1
}

# Criar diretórios do Mosquitto se não existirem
$mosquittoPath = "mosquitto"
$mosquittoConfig = Join-Path $mosquittoPath "config"
$mosquittoData = Join-Path $mosquittoPath "data"
$mosquittoLog = Join-Path $mosquittoPath "log"

@($mosquittoConfig, $mosquittoData, $mosquittoLog) | ForEach-Object {
    if (!(Test-Path $_)) {
        New-Item -ItemType Directory -Path $_ -Force | Out-Null
        Write-Host "📁 Criado diretório: $_" -ForegroundColor Green
    }
}

# Verificar se o arquivo de configuração do Mosquitto existe
$mosquittoConfigFile = Join-Path $mosquittoConfig "mosquitto.conf"
if (!(Test-Path $mosquittoConfigFile)) {
    Write-Host "⚠️  Arquivo de configuração do Mosquitto não encontrado." -ForegroundColor Yellow
    Write-Host "   Por favor, verifique o arquivo em: $mosquittoConfigFile" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "🏗️  Escolha o modo de execução:" -ForegroundColor Cyan
Write-Host "1. Docker Compose (Recomendado - inclui Mosquitto)" -ForegroundColor White
Write-Host "2. Desenvolvimento local (.NET CLI)" -ForegroundColor White
Write-Host "3. Apenas Mosquitto via Docker" -ForegroundColor White
Write-Host "4. Parar todos os serviços" -ForegroundColor White
Write-Host ""

$choice = Read-Host "Digite sua escolha (1-4)"

switch ($choice) {
    "1" {
        Write-Host ""
        Write-Host "🐳 Iniciando ambiente completo com Docker Compose..." -ForegroundColor Green
        
        # Build e start dos containers
        docker-compose down --remove-orphans
        docker-compose build
        docker-compose up -d
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "✅ Ambiente iniciado com sucesso!" -ForegroundColor Green
            Write-Host ""
            Write-Host "🌐 Serviços disponíveis:" -ForegroundColor Cyan
            Write-Host "   • API: https://localhost:8081" -ForegroundColor White
            Write-Host "   • Swagger: https://localhost:8081/swagger" -ForegroundColor White
            Write-Host "   • SignalR Hub: https://localhost:8081/hub" -ForegroundColor White
            Write-Host "   • MQTT Broker: localhost:1883" -ForegroundColor White
            Write-Host "   • MQTT WebSocket: localhost:9001" -ForegroundColor White
            Write-Host ""
            Write-Host "📄 Para ver logs: docker-compose logs -f" -ForegroundColor Yellow
            Write-Host "🛑 Para parar: docker-compose down" -ForegroundColor Yellow
        } else {
            Write-Host "❌ Erro ao iniciar o ambiente." -ForegroundColor Red
        }
    }
    
    "2" {
        Write-Host ""
        Write-Host "💻 Iniciando desenvolvimento local..." -ForegroundColor Green
        
        # Verificar se há uma instância do Mosquitto rodando
        Write-Host "⚠️  Certifique-se de que há um servidor MQTT rodando em localhost:1883" -ForegroundColor Yellow
        Write-Host "   Você pode usar: docker run -it -p 1883:1883 eclipse-mosquitto" -ForegroundColor Yellow
        Write-Host ""
        
        $proceed = Read-Host "Continuar? (y/N)"
        if ($proceed -eq "y" -or $proceed -eq "Y") {
            Set-Location "MqttGateway.Server"
            Write-Host "🔨 Restaurando pacotes NuGet..." -ForegroundColor Yellow
            dotnet restore
            
            Write-Host "🚀 Iniciando aplicação..." -ForegroundColor Yellow
            dotnet run
        }
    }
    
    "3" {
        Write-Host ""
        Write-Host "🦟 Iniciando apenas Mosquitto..." -ForegroundColor Green
        
        docker run -d `
            --name mosquitto-standalone `
            -p 1883:1883 `
            -p 9001:9001 `
            -v "${PWD}/mosquitto/config:/mosquitto/config" `
            -v "${PWD}/mosquitto/data:/mosquitto/data" `
            -v "${PWD}/mosquitto/log:/mosquitto/log" `
            eclipse-mosquitto
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Mosquitto iniciado com sucesso!" -ForegroundColor Green
            Write-Host "   MQTT: localhost:1883" -ForegroundColor White
            Write-Host "   WebSocket: localhost:9001" -ForegroundColor White
        } else {
            Write-Host "❌ Erro ao iniciar Mosquitto." -ForegroundColor Red
        }
    }
    
    "4" {
        Write-Host ""
        Write-Host "🛑 Parando serviços..." -ForegroundColor Yellow
        
        # Parar Docker Compose
        docker-compose down --remove-orphans
        
        # Parar container standalone se existir
        docker stop mosquitto-standalone 2>$null
        docker rm mosquitto-standalone 2>$null
        
        Write-Host "✅ Serviços parados." -ForegroundColor Green
    }
    
    default {
        Write-Host "❌ Opção inválida." -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "📚 Recursos úteis:" -ForegroundColor Cyan
Write-Host "   • Cliente de exemplo: ../Examples/Client/index.html" -ForegroundColor White
Write-Host "   • Documentação: ../README.md" -ForegroundColor White
Write-Host "   • Logs do Mosquitto: ./mosquitto/log/" -ForegroundColor White
Write-Host ""
Write-Host "🎉 Setup concluído!" -ForegroundColor Green