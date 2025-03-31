// index.js
const express = require("express");
const cors = require("cors");   // ✅ ajout pour CORS
const http = require("http");
const { Server } = require("socket.io");
const gameModule = require("./game");
const chatModule = require("./chat");
const store = require("./store"); // <-- Pour accéder à globalPlayers

const app = express();
app.use(cors());                // ✅ activation CORS globale


const server = http.createServer(app);
const io = new Server(server, {
  cors: { origin: "*" },
});

// Route GET simple pour vérifier que le serveur répond
app.get("/", (req, res) => {
  res.send("Serveur Socket.IO en ligne !");
});

// =============================
// Nouvelle route pour la liste des joueurs à afficher sur le site de monitoring
// =============================
app.get("/players", (req, res) => {
  // Construire un tableau { id, pseudo } pour chaque joueur
  const playersArray = Object.entries(store.globalPlayers).map(([id, pseudo]) => ({
    id,
    pseudo,
  }));

  // Renvoyer au format JSON
  res.json({
    players: playersArray,
  });
});


// On branche nos modules en leur passant 'io'
gameModule(io);
chatModule(io);

const PORT = process.env.PORT || 3000;
server.listen(PORT, () => {
  console.log(`🚀 Serveur lancé sur le port ${PORT}`);
});