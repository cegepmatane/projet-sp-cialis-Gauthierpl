const express = require('express');
const http = require('http');
const { Server } = require('socket.io');

const app = express();
const server = http.createServer(app);
const io = new Server(server, {
  cors: { origin: "*" }
});

app.get('/', (req, res) => {
  res.send("Serveur Socket.IO en ligne !");
});

const GLOBAL_ROOM = "globalRoom";

// Stocke les joueurs : { socketId: pseudo }
const globalPlayers = {};

function emitPlayersList() {
  // Envoi d'une liste de joueurs (id et pseudo) – optionnel pour l'UI
  const playersList = [];
  for (const id in globalPlayers) {
    playersList.push({ id, pseudo: globalPlayers[id] });
  }
  io.in(GLOBAL_ROOM).emit('playersList', { players: playersList });
}

io.on('connection', (socket) => {
  console.log(`🟢 [${socket.id}] Nouveau client connecté`);

  socket.on('joinGame', (data) => {
    const pseudo = data.pseudo;
    if (!pseudo) {
      console.log(`Pseudo manquant pour ${socket.id}`);
      return;
    }
    
    // Enregistrer le pseudo et rejoindre la salle globale
    globalPlayers[socket.id] = pseudo;
    socket.join(GLOBAL_ROOM);
    console.log(`🔑 Joueur ${socket.id} (${pseudo}) a rejoint le salon global: ${GLOBAL_ROOM}`);
    
    // Envoyer la confirmation au client en incluant son id
    socket.emit('gameJoined', { room: GLOBAL_ROOM, id: socket.id });
    
    // Envoyer la liste mise à jour à tous
    emitPlayersList();
    
    // Informer tous les clients (y compris le nouveau) pour spawn ce joueur
    console.log(`[${socket.id}] Émission de spawnPlayer pour le pseudo "${pseudo}"`);
    io.in(GLOBAL_ROOM).emit('spawnPlayer', { id: socket.id, pseudo: pseudo });
  });

  // Réception de la nouvelle position d'un joueur
  socket.on('playerMove', (data) => {
    console.log(`[playerMove] from ${socket.id}:`, data);
    // Optionnel : parse localement pour vérifier
    const parsedX = parseFloat(data.x);
    if (isNaN(parsedX)) {
      console.warn(`!!! Mauvaise donnée x: ${data.x} (type: ${typeof data.x})`);
      return; // on ne broadcast pas si c'est invalide
    }
  
    console.log(`[playerMove] x = ${parsedX} -> broadcast updatePlayer`);
    socket.broadcast.to(GLOBAL_ROOM).emit('updatePlayer', { id: socket.id, x: parsedX });
  });
  

  // Quand on reçoit la demande getPlayersList
  socket.on('getPlayersList', () => {
    console.log(`[${socket.id}] getPlayersList request received.`);
    const playersList = [];
    for (const id in globalPlayers) {
      playersList.push({ id: id, pseudo: globalPlayers[id] });
    }
    socket.emit('playersList', { players: playersList });
  });

  socket.on('disconnect', () => {
    console.log(`🔴 [${socket.id}] Déconnexion...`);
    if (globalPlayers[socket.id]) {
      delete globalPlayers[socket.id];
      emitPlayersList();
      // Notifier les clients pour retirer ce joueur
      io.in(GLOBAL_ROOM).emit('removePlayer', { id: socket.id });
    }
  });
});

const PORT = 3000;
server.listen(PORT, () => {
  console.log(`🚀 Serveur lancé sur le port ${PORT}`);
});
