// Fichier: Script serveur/store.js
module.exports = {
  globalPlayers: {},         // { socketId: pseudo }
  GLOBAL_ROOM: "globalRoom",
  availableMaps: [],         // Stockera les JSON des cartes [{ id: 1, mapCode: "{...}" }, ...]
  currentMapIndex: -1,       // Index de la carte actuelle
  currentMapJson: null,      // JSON brut de la carte actuelle
  currentSpawnPoint: { x: 0, y: 0, z: 0 }, // Coordonnées du spawn de la carte actuelle
  deadPlayers: new Set(),    // Ensemble des IDs des joueurs morts en attente de respawn
};