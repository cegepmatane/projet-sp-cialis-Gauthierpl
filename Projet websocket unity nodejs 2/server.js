/*const express = require('express');
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
  // Envoi d'une liste de joueurs (id et pseudo)
  // FONCTION optionnelle si vous en avez besoin au moment de la DECONNEXION
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
    console.log(`🔑 Joueur ${socket.id} (${pseudo}) a rejoint la salle: ${GLOBAL_ROOM}`);

    // Envoyer la confirmation au client en incluant son id
    socket.emit('gameJoined', { room: GLOBAL_ROOM, id: socket.id });

    // Au lieu de broadcast la liste à tout le salon, on envoie SEULEMENT au nouveau joueur :
    const playersList = [];
    for (const id in globalPlayers) {
      playersList.push({ id, pseudo: globalPlayers[id] });
    }
    socket.emit('playersList', { players: playersList });

    // IMPORTANT : on ne fait PLUS de io.in(GLOBAL_ROOM).emit('spawnPlayer', ...);
    // pour éviter le double spawn
  });

  // Nouveau listener: "playerReady" → le joueur informe qu’il a chargé sa scène
  socket.on('playerReady', () => {
    console.log(`[playerReady] Le joueur ${socket.id} est prêt. Émission spawnPlayer vers tous.`);
    if (globalPlayers[socket.id]) {
      io.in(GLOBAL_ROOM).emit('spawnPlayer', {
        id: socket.id,
        pseudo: globalPlayers[socket.id]
      });
    }
  });

  // Réception de la nouvelle position d'un joueur
  socket.on('playerMove', (data) => {
    console.log(`[playerMove] from ${socket.id}:`, data);
    const parsedX = parseFloat(data.x);
    const parsedY = parseFloat(data.y);

    if (isNaN(parsedX) || isNaN(parsedY)) {
      console.warn(`!!! Mauvaise donnée x: ${data.x} ou y: ${data.y}`);
      return; // Ne pas broadcast si les valeurs sont invalides
    }

    // Transmettre la position aux autres joueurs
    socket.broadcast.to(GLOBAL_ROOM).emit('updatePlayer', {
      id: socket.id,
      x: parsedX,
      y: parsedY
    });
  });

  // Quand on reçoit la demande getPlayersList
  // (Le nouveau joueur peut demander à nouveau la liste, ou vous pouvez ignorer si vous en avez plus besoin)
  socket.on('getPlayersList', () => {
    console.log(`[${socket.id}] getPlayersList request received.`);
    const playersList = [];
    for (const id in globalPlayers) {
      playersList.push({ id, pseudo: globalPlayers[id] });
    }
    socket.emit('playersList', { players: playersList });
  });

  // Déconnexion
  socket.on('disconnect', () => {
    console.log(`🔴 [${socket.id}] Déconnexion...`);
    if (globalPlayers[socket.id]) {
      delete globalPlayers[socket.id];

      // Si vous voulez broadcast la liste mise à jour, ok :
      // emitPlayersList();

      // Notifier les clients pour retirer ce joueur
      io.in(GLOBAL_ROOM).emit('removePlayer', { id: socket.id });
    }
  });
});

const PORT = 3000;
server.listen(PORT, () => {
  console.log(`🚀 Serveur lancé sur le port ${PORT}`);
});




*/