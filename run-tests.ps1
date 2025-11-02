# MQTT Gateway Tests - PowerShell Test Runner
param(
    [string]$TestType = "all",
    [switch]$Coverage = $false,
    [switch]$Verbose = $false,
    [string]$Filter = ""
)

Write-Host "MQTT Gateway Server - Test Runner" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green
Write-Host ""

# Navegar para o diretorio de testes
$TestsPath = Join-Path $PSScriptRoot "Solution\MqttGateway.Tests"
if (!(Test-Path $TestsPath)) {
    Write-Host "Diretorio de testes nao encontrado: $TestsPath" -ForegroundColor Red
    exit 1
}

Set-Location $TestsPath

# Verificar se o .NET esta disponivel
try {
    $dotnetVersion = dotnet --version
    Write-Host ".NET encontrado: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host ".NET nao encontrado. Instale o .NET 8 SDK." -ForegroundColor Red
    exit 1
}

# Funcao para executar testes
function Invoke-TestRun {
    param(
        [string]$Category,
        [string]$FilterExpression,
        [string]$DisplayName
    )
    
    Write-Host ""
    Write-Host "Executando $DisplayName..." -ForegroundColor Cyan
    
    $testArgs = @("test")
    
    if ($Coverage) {
        $testArgs += "--collect:XPlat Code Coverage"
    }
    
    if ($Verbose) {
        $testArgs += "--verbosity", "detailed"
    } else {
        $testArgs += "--verbosity", "normal"
    }
    
    if ($FilterExpression) {
        $testArgs += "--filter", $FilterExpression
    }
    
    $testArgs += "--logger", "console;verbosity=normal"
    
    Write-Host "Comando: dotnet $($testArgs -join ' ')" -ForegroundColor Gray
    
    & dotnet @testArgs
    $testResult = $LASTEXITCODE
    
    if ($testResult -eq 0) {
        Write-Host "$DisplayName concluidos com sucesso" -ForegroundColor Green
    } else {
        Write-Host "$DisplayName falharam (codigo: $testResult)" -ForegroundColor Red
    }
    
    return $testResult
}

# Restaurar pacotes NuGet
Write-Host "Restaurando pacotes NuGet..." -ForegroundColor Yellow
dotnet restore --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "Falha ao restaurar pacotes NuGet" -ForegroundColor Red
    exit 1
}

# Compilar projeto de testes
Write-Host "Compilando projeto de testes..." -ForegroundColor Yellow
dotnet build --no-restore --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "Falha na compilacao" -ForegroundColor Red
    exit 1
}

$overallResult = 0

# Executar testes baseado no tipo solicitado
switch ($TestType.ToLower()) {
    "unit" {
        $result = Invoke-TestRun "Unit" "FullyQualifiedName~Unit" "Testes Unitarios"
        $overallResult = [Math]::Max($overallResult, $result)
    }
    
    "integration" {
        $result = Invoke-TestRun "Integration" "FullyQualifiedName~Integration" "Testes de Integracao"
        $overallResult = [Math]::Max($overallResult, $result)
    }
    
    "performance" {
        $result = Invoke-TestRun "Performance" "FullyQualifiedName~Performance" "Testes de Performance"
        $overallResult = [Math]::Max($overallResult, $result)
    }
    
    "e2e" {
        $result = Invoke-TestRun "EndToEnd" "FullyQualifiedName~EndToEnd" "Testes End-to-End"
        $overallResult = [Math]::Max($overallResult, $result)
    }
    
    "smoke" {
        $smokeFilter = "FullyQualifiedName~Unit"
        $result = Invoke-TestRun "Smoke" $smokeFilter "Testes de Smoke"
        $overallResult = [Math]::Max($overallResult, $result)
    }
    
    "all" {
        Write-Host "Executando suite completa de testes..." -ForegroundColor Cyan
        
        # 1. Testes Unitarios
        $result = Invoke-TestRun "Unit" "FullyQualifiedName~Unit" "Testes Unitarios"
        $overallResult = [Math]::Max($overallResult, $result)
        
        if ($result -eq 0) {
            # 2. Testes de Integracao
            $result = Invoke-TestRun "Integration" "FullyQualifiedName~Integration" "Testes de Integracao"
            $overallResult = [Math]::Max($overallResult, $result)
            
            if ($result -eq 0) {
                # 3. Testes End-to-End
                $result = Invoke-TestRun "EndToEnd" "FullyQualifiedName~EndToEnd" "Testes End-to-End"
                $overallResult = [Math]::Max($overallResult, $result)
            }
        }
    }
    
    "custom" {
        if ([string]::IsNullOrEmpty($Filter)) {
            Write-Host "Para testes customizados, use o parametro -Filter" -ForegroundColor Red
            exit 1
        }
        
        $result = Invoke-TestRun "Custom" $Filter "Testes Customizados"
        $overallResult = [Math]::Max($overallResult, $result)
    }
    
    default {
        Write-Host "Tipo de teste invalido: $TestType" -ForegroundColor Red
        Write-Host "Tipos validos: unit, integration, performance, e2e, smoke, all, custom" -ForegroundColor Yellow
        exit 1
    }
}

# Resumo final
Write-Host ""
Write-Host "Resumo da Execucao" -ForegroundColor Cyan
Write-Host "Tipo de teste: $TestType" -ForegroundColor White

if ($overallResult -eq 0) {
    Write-Host "Todos os testes executados com sucesso!" -ForegroundColor Green
} else {
    Write-Host "Alguns testes falharam. Verifique a saida acima." -ForegroundColor Red
}

Write-Host ""
Write-Host "Comandos uteis:" -ForegroundColor Cyan
Write-Host "  Unit tests:        .\run-tests.ps1 -TestType unit" -ForegroundColor Gray
Write-Host "  Integration:       .\run-tests.ps1 -TestType integration" -ForegroundColor Gray
Write-Host "  With coverage:     .\run-tests.ps1 -TestType all -Coverage" -ForegroundColor Gray

exit $overallResult