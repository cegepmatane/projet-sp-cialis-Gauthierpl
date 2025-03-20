// game.js
const store = require("./store"); // On importe globalPlayers, GLOBAL_ROOM

module.exports = function (io) {
  // Quand un nouveau client se connecte
  io.on("connection", (socket) => {
    console.log(`[game] 🟢 [${socket.id}] Nouveau client connecté`);

    // joinGame
    socket.on("joinGame", (data) => {
      const pseudo = data.pseudo;
      if (!pseudo) {
        console.log(`[game] Pseudo manquant pour ${socket.id}`);
        return;
      }

      // Enregistrer le pseudo et rejoindre la salle globale
      store.globalPlayers[socket.id] = pseudo;
      socket.join(store.GLOBAL_ROOM);
      console.log(`[game] 🔑 Joueur ${socket.id} (${pseudo}) a rejoint la salle: ${store.GLOBAL_ROOM}`);

      // Envoyer la confirmation au client (son propre id + room)
      socket.emit("gameJoined", { room: store.GLOBAL_ROOM, id: socket.id });

      // Envoyer la liste de joueurs existants AU NOUVEAU joueur
      const playersList = Object.entries(store.globalPlayers).map(([id, pseu]) => ({
        id,
        pseudo: pseu,
      }));
      socket.emit("playersList", { players: playersList });

      // IMPORTANT : ne pas broadcast spawnPlayer ici pour éviter le double spawn
    });

    // playerReady
    socket.on("playerReady", () => {
      if (store.globalPlayers[socket.id]) {
        console.log(`[game] [playerReady] Joueur ${socket.id} est prêt. On spawn côté autres.`);
        io.in(store.GLOBAL_ROOM).emit("spawnPlayer", {
          id: socket.id,
          pseudo: store.globalPlayers[socket.id],
        });
      }
    });

    // playerMove
    socket.on("playerMove", (data) => {
      const parsedX = parseFloat(data.x);
      const parsedY = parseFloat(data.y);
      // Récupération des booléens envoyés par le client
      const isRunning = data.isRunning;
      const isIdle = data.isIdle;
   
      if (isNaN(parsedX) || isNaN(parsedY)) {
        console.warn(`[game] Mauvaise donnée x: ${data.x}, y: ${data.y}`);
        return;
      }
    
      
      // Broadcast la position + animation aux autres
      socket.broadcast.to(store.GLOBAL_ROOM).emit("updatePlayer", {
        id: socket.id,
        x: parsedX,
        y: parsedY,
        isRunning: isRunning,
        isIdle: isIdle,
      });
    });
    

    // getPlayersList (si besoin de récupérer la liste plus tard)
    socket.on("getPlayersList", () => {
      const playersList = Object.entries(store.globalPlayers).map(([id, pseu]) => ({
        id,
        pseudo: pseu,
      }));
      // Envoyer la liste de joueurs existants AU NOUVEAU joueur
      // socket.emit("playersList", { players: playersList });

      // Envoyer la liste à tous les joueurs déjà présents
      io.in(store.GLOBAL_ROOM).emit("playersList", { players: playersList });
    });

    // Déconnexion
    socket.on("disconnect", () => {
      console.log(`[game] 🔴 [${socket.id}] Déconnexion...`);
      if (store.globalPlayers[socket.id]) {
        delete store.globalPlayers[socket.id];

        // Notifier les clients pour retirer ce joueur
        io.in(store.GLOBAL_ROOM).emit("removePlayer", { id: socket.id });


        // Émettre la liste actualisée des joueurs
        const playersList = Object.entries(store.globalPlayers).map(([id, pseu]) => ({
          id,
          pseudo: pseu,
        }));

        io.in(store.GLOBAL_ROOM).emit("playersList", { players: playersList });
      }
    });
  });
};
