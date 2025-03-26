// index.js
const express = require("express");
const http = require("http");
const { Server } = require("socket.io");
const gameModule = require("./game");
const chatModule = require("./chat");

const app = express();
const server = http.createServer(app);
const io = new Server(server, {
  cors: { origin: "*" },
});

// Route GET simple pour vérifier que le serveur répond
app.get("/", (req, res) => {
  res.send("Serveur Socket.IO en ligne !");
});

// On branche nos modules en leur passant 'io'
gameModule(io);
chatModule(io);

const PORT = process.env.PORT || 3000;
server.listen(PORT, () => {
  console.log(`🚀 Serveur lancé sur le port ${PORT}`);
});
