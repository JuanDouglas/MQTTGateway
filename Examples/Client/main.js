let connection = null;
let currentSessionId = null;

// Gerar UUID v4
function generateUUID() {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (c) {
    var r = (Math.random() * 16) | 0,
      v = c == "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

// Atualizar status da conexão
function updateConnectionStatus(connected, message = null) {
  const statusElement = document.getElementById("connectionStatus");
  const connectBtn = document.getElementById("connectBtn");
  const sendBtn = document.getElementById("sendBtn");

  if (connected) {
    statusElement.className = "status connected";
    statusElement.innerHTML = `✅ Conectado - Session: ${currentSessionId}`;
    connectBtn.textContent = "Desconectar";
    sendBtn.disabled = false;
  } else {
    statusElement.className = "status disconnected";
    statusElement.innerHTML = message || "❌ Desconectado";
    connectBtn.textContent = "Conectar";
    sendBtn.disabled = true;
  }
}

// Adicionar mensagem à lista
function addMessage(content, type = "received", channel = null) {
  const messagesDiv = document.getElementById("messages");

  // Remover mensagem "Nenhuma mensagem"
  if (
    messagesDiv.children.length === 1 &&
    messagesDiv.children[0].tagName === "P"
  ) {
    messagesDiv.innerHTML = "";
  }

  const messageDiv = document.createElement("div");
  messageDiv.className = `message ${type}`;

  const timestamp = new Date().toLocaleTimeString("pt-BR");
  const channelText = channel ? ` [${channel}]` : "";

  messageDiv.innerHTML = `
                <div class="message-header">
                    ${timestamp} - ${
    type === "context" ? "Contexto" : "Nova Mensagem"
  }${channelText}
                </div>
                <div>${
                  typeof content === "string"
                    ? content
                    : JSON.stringify(content, null, 2)
                }</div>
            `;

  messagesDiv.appendChild(messageDiv);
  messagesDiv.scrollTop = messagesDiv.scrollHeight;
}

// Limpar mensagens
function clearMessages() {
  document.getElementById("messages").innerHTML =
    "<p>Nenhuma mensagem ainda...</p>";
}

// Conectar/Desconectar
async function toggleConnection() {
  if (connection && connection.state === signalR.HubConnectionState.Connected) {
    await connection.stop();
    return;
  }

  const serverUrl = document.getElementById("serverUrl").value;
  let sessionId = document.getElementById("sessionId").value.trim();

  if (!sessionId) {
    sessionId = generateUUID();
    document.getElementById("sessionId").value = sessionId;
  }

  currentSessionId = sessionId;

  // Construir URL com sessionId
  const hubUrl = `${serverUrl}?sessionId=${sessionId}`;

  try {
    updateConnectionStatus(false, "🔄 Conectando...");

    connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .build();

    // Eventos da conexão
    connection.on("SetContext", (context) => {
      console.log("Contexto recebido:", context);
      addMessage(context, "context");
    });

    connection.on("ReceiveMessage", (data) => {
      console.log("Mensagem recebida:", data);
      addMessage(data, "received");
    });

    connection.onclose(() => {
      updateConnectionStatus(false, "❌ Conexão perdida");
    });

    connection.onreconnecting(() => {
      updateConnectionStatus(false, "🔄 Reconectando...");
    });

    connection.onreconnected(() => {
      updateConnectionStatus(true);
    });

    await connection.start();
    updateConnectionStatus(true);
  } catch (error) {
    console.error("Erro na conexão:", error);
    updateConnectionStatus(false, `❌ Erro: ${error.message}`);
  }
}

// Enviar mensagem via API REST
async function sendMessage() {
  const message = document.getElementById("messageText").value.trim();
  const channel = document.getElementById("channelText").value.trim() || null;
  const targetId = document.getElementById("targetId").value.trim() || null;

  if (!message) {
    alert("Digite uma mensagem antes de enviar!");
    return;
  }

  if (!currentSessionId) {
    alert("Conecte-se primeiro!");
    return;
  }

  try {
    const serverBase = document
      .getElementById("serverUrl")
      .value.replace("/hub", "");
    const response = await fetch(`${serverBase}/Messages/Send`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        sessionId: currentSessionId,
        message: message,
        targetId: targetId,
        channel: channel,
      }),
    });

    if (response.ok) {
      document.getElementById("messageText").value = "";
      addMessage(`Enviado: ${message}`, "sent", channel);
    } else {
      throw new Error(`HTTP ${response.status}`);
    }
  } catch (error) {
    console.error("Erro ao enviar mensagem:", error);
    alert(`Erro ao enviar mensagem: ${error.message}`);
  }
}

// Permitir envio com Enter
document
  .getElementById("messageText")
  .addEventListener("keypress", function (e) {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  });

// Gerar sessionId automático na inicialização
document.addEventListener("DOMContentLoaded", function () {
  if (!document.getElementById("sessionId").value) {
    document.getElementById("sessionId").value = generateUUID();
  }
});
