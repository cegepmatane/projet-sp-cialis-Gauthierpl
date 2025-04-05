// Fichier: Script serveur/index.js
const express = require("express");
const cors = require("cors");
const http = require("http");
const { Server } = require("socket.io");
const gameModule = require("./game");
const chatModule = require("./chat");
const mapManager = require("./map_manager"); // <-- Ajout
const store = require("./store");

const app = express();
app.use(cors());

const server = http.createServer(app);
const io = new Server(server, {
  cors: { origin: "*" },
});

// Route GET simple pour vérifier que le serveur répond
app.get("/", (req, res) => {
  res.send("Serveur Socket.IO en ligne !");
});

// Route pour la liste des joueurs
app.get("/players", (req, res) => {
  const playersArray = Object.entries(store.globalPlayers).map(([id, pseudo]) => ({
    id,
    pseudo,
  }));
  res.json({
    players: playersArray,
  });
});

// On branche nos modules Socket.IO
gameModule(io);
chatModule(io);

// On démarre la rotation des cartes après l'initialisation de 'io'
mapManager.startMapRotation(io); // <-- Ajout

const PORT = process.env.PORT || 3000;
server.listen(PORT, () => {
  console.log(`🚀 Serveur lancé sur le port ${PORT}`);
});