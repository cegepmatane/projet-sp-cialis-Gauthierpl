// chat.js
const store = require("./store");

module.exports = function (io) {
  io.on("connection", (socket) => {
    console.log(`[chat] Connexion socket ${socket.id}`);

    // Lorsqu'on reçoit un message de chat
    socket.on("sendChatMessage", (msgData) => {
      // msgData : { message: "Hello world", ... }
      const pseudo = store.globalPlayers[socket.id];
      if (!pseudo) {
        console.log(`[chat] Erreur : pseudo introuvable pour socket.id=${socket.id}`);
        return;
      }

      // Construire l'objet à diffuser
      const chatMsg = {
        id: socket.id,
        pseudo: pseudo,
        message: msgData.message,
        time: Date.now(), // timestamp si tu veux
      };

      // Émettre le message à tout le salon
      io.in(store.GLOBAL_ROOM).emit("chatMessage", chatMsg);
      console.log(`[chat] ${pseudo} dit: ${msgData.message}`);
    });
  });
};
