// game.js
const fs = require('fs');
const path = require('path');
const store = require("./store"); // On importe globalPlayers, GLOBAL_ROOM

// chemin du fichier de log (au même endroit que game.js)
const logFilePath = path.join(__dirname, 'connections-log.json');

// Fonction pour logger une connexion ou déconnexion
function logConnection(id, pseudo, action) {
  const logs = fs.existsSync(logFilePath) ? JSON.parse(fs.readFileSync(logFilePath)) : [];

  if (action === "join") {
    logs.push({ id, pseudo, joinedAt: new Date().toISOString(), disconnectedAt: null });
  } else if (action === "leave") {
    const log = logs.find(log => log.id === id && log.disconnectedAt === null);
    if (log) log.disconnectedAt = new Date().toISOString();
  }

  fs.writeFileSync(logFilePath, JSON.stringify(logs, null, 2));
}

module.exports = function (io) {
  io.on("connection", (socket) => {
    console.log(`[game] 🟢 [${socket.id}] Nouveau client connecté`);

    socket.on("joinGame", (data) => {
      const pseudo = data.pseudo;
      if (!pseudo) {
        console.log(`[game] Pseudo manquant pour ${socket.id}`);
        return;
      }

      store.globalPlayers[socket.id] = pseudo;
      socket.join(store.GLOBAL_ROOM);
      console.log(`[game] 🔑 Joueur ${socket.id} (${pseudo}) a rejoint la salle: ${store.GLOBAL_ROOM}`);

      socket.emit("gameJoined", { room: store.GLOBAL_ROOM, id: socket.id });

      const playersList = Object.entries(store.globalPlayers).map(([id, pseu]) => ({
        id,
        pseudo: pseu,
      }));
      socket.emit("playersList", { players: playersList });

      // Log connexion
      logConnection(socket.id, pseudo, "join");
    });

    socket.on("playerReady", () => {
      if (store.globalPlayers[socket.id]) {
        io.in(store.GLOBAL_ROOM).emit("spawnPlayer", {
          id: socket.id,
          pseudo: store.globalPlayers[socket.id],
        });
      }
    });

    socket.on("playerMove", (data) => {
      const parsedX = parseFloat(data.x);
      const parsedY = parseFloat(data.y);
      const { isRunning, isIdle, flip } = data; // récupération de "flip"
    
      if (isNaN(parsedX) || isNaN(parsedY)) {
        console.warn(`[game] Mauvaise donnée x: ${data.x}, y: ${data.y}`);
        return;
      }
    
      socket.broadcast.to(store.GLOBAL_ROOM).emit("updatePlayer", {
        id: socket.id, 
        x: parsedX, 
        y: parsedY, 
        isRunning, 
        isIdle,
        flip // on transmet cette information aux autres clients
      });
    });
    

    socket.on("getPlayersList", () => {
      const playersList = Object.entries(store.globalPlayers).map(([id, pseu]) => ({
        id,
        pseudo: pseu,
      }));
      io.in(store.GLOBAL_ROOM).emit("playersList", { players: playersList });
    });

    socket.on("disconnect", () => {
      console.log(`[game] 🔴 [${socket.id}] Déconnexion...`);
      const pseudo = store.globalPlayers[socket.id];

      if (pseudo) {
        delete store.globalPlayers[socket.id];
        io.in(store.GLOBAL_ROOM).emit("removePlayer", { id: socket.id });

        const playersList = Object.entries(store.globalPlayers).map(([id, pseu]) => ({
          id,
          pseudo: pseu,
        }));

        io.in(store.GLOBAL_ROOM).emit("playersList", { players: playersList });

        // Log déconnexion
        logConnection(socket.id, pseudo, "leave");
      }
    });
  });
};
