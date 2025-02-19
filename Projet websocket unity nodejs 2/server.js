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

// On stocke les salons dans un objet.
// rooms = { salonName: { players: [socketId, ...], createdAt: Date } }
const rooms = {};

io.on('connection', (socket) => {
  console.log(`🟢 [${socket.id}] Nouveau client connecté`);

  // === Événement: createRoom
  // Le client envoie { roomName: "nomDuSalon" }
  socket.on('createRoom', (data) => {
    console.log(`📥 createRoom reçu de ${socket.id} avec data:`, data);

    const roomName = data.roomName;
    if (!roomName) return;

    if (!rooms[roomName]) {
        rooms[roomName] = { players: [], createdAt: new Date() };
        console.log(`🏠 Salon créé: ${roomName}`);
    } else {
        console.log(`🏠 Salon existant rejoint: ${roomName}`);
    }

    rooms[roomName].players.push(socket.id);
    socket.join(roomName);

    // Log côté serveur avant envoi
    console.log(`📤 Envoi de 'roomCreated' à ${socket.id}`);

    socket.emit('roomCreated', roomName);
});

socket.on('joinRoom', (data) => {
    console.log(`📥 joinRoom reçu de ${socket.id} avec data:`, data);

    const roomName = data.roomName;
    if (!roomName) return;

    if (!rooms[roomName]) {
        console.log(`⚠️ Tentative de rejoindre un salon inexistant: ${roomName}`);
        socket.emit('roomError', { error: 'Room not found!' });
        return;
    }

    rooms[roomName].players.push(socket.id);
    socket.join(roomName);
    console.log(`🔑 Joueur ${socket.id} a rejoint le salon: ${roomName}`);

    console.log(`📤 Envoi de 'roomJoined' à ${socket.id}`);
    socket.emit('roomJoined', roomName);
});

socket.on('getRooms', () => {
    console.log(`📥 getRooms reçu de ${socket.id}`);
    const roomList = Object.keys(rooms);
    console.log(`📋 Liste des salons envoyée: ${roomList}`);

    console.log(`📤 Envoi de 'roomsList' à ${socket.id}`);
    socket.emit('roomsList', { rooms: roomList });
});



  // === Événement: disconnect
  socket.on('disconnect', () => {
    console.log(`🔴 [${socket.id}] Déconnexion...`);

    // Supprimer le joueur de tous les salons
    for (const roomName in rooms) {
      const index = rooms[roomName].players.indexOf(socket.id);
      if (index !== -1) {
        rooms[roomName].players.splice(index, 1);
        // S'il n'y a plus personne dans le salon, on supprime ce salon
        if (rooms[roomName].players.length === 0) {
          delete rooms[roomName];
          console.log(`🗑️ Salon supprimé: ${roomName} (plus de joueurs)`);
        }
      }
    }
  });
});

const PORT = 3000;
server.listen(PORT, () => {
  console.log(`🚀 Serveur lancé sur le port ${PORT}`);
});
