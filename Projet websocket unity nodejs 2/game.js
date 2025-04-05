// Fichier: Script serveur/game.js
const fs = require('fs');
const path = require('path');
const store = require("./store");
// const logFilePath = path.join(__dirname, 'connections-log.json'); // Log désactivé

// --- Horloge de Respawn (MODIFIÉE) ---
const RESPAWN_INTERVAL_MS = 2000; // 2 secondes
let respawnIntervalId = null;

function startRespawnClock(io) {
    if (respawnIntervalId) {
        clearInterval(respawnIntervalId);
    }
    console.log(`[Game] Horloge de respawn démarrée (intervalle: ${RESPAWN_INTERVAL_MS}ms)`);
    respawnIntervalId = setInterval(() => {
        if (store.deadPlayers.size > 0) {
             console.log(`[Game] Traitement respawn pour ${store.deadPlayers.size} joueur(s) dans deadPlayers: [${Array.from(store.deadPlayers).join(', ')}]`);
            // Copie pour itération sûre
            const playersToRespawn = new Set(store.deadPlayers);
            store.deadPlayers.clear(); // Vide l'original pour le prochain cycle

            playersToRespawn.forEach(playerId => {
                const pseudo = store.globalPlayers[playerId]; // Récupère le pseudo pour log et l'envoi
                if (pseudo) { // Vérifie si le joueur est toujours dans globalPlayers (donc connecté)

                    // <<< MODIFICATION: Diffuser 'spawnPlayer' à TOUT LE MONDE avec la position >>>
                    const spawnData = {
                        id: playerId,
                        pseudo: pseudo,
                        spawnPoint: store.currentSpawnPoint // Le point de spawn actuel de la carte
                    };
                    console.log(`[Game] Broadcast spawnPlayer pour le respawn de ${pseudo} (${playerId}) au point`, store.currentSpawnPoint);
                    io.in(store.GLOBAL_ROOM).emit('spawnPlayer', spawnData);
                    // On n'envoie plus 'respawnPlayer' spécifiquement ni 'playerStateUpdate'

                } else {
                    console.log(`[Game] Joueur ${playerId} (pseudo inconnu) à respawn mais déconnecté ou introuvable dans globalPlayers.`);
                }
            });
        }
    }, RESPAWN_INTERVAL_MS);
}
// -------------------------


module.exports = function (io) {

    // Démarrer l'horloge de respawn une seule fois
    if (!respawnIntervalId) {
        startRespawnClock(io);
    }

    io.on("connection", (socket) => {
        console.log(`[game] 🟢 [${socket.id}] Nouveau client connecté`);

        // --- Rejoindre le jeu ---
        socket.on("joinGame", (data) => {
            const pseudo = data.pseudo ? data.pseudo.trim() : null; // Nettoyer le pseudo
            if (!pseudo) {
                console.log(`[game] ⚠️ Pseudo manquant ou vide pour ${socket.id}. Déconnexion.`);
                 socket.emit("joinError", { message: "Le pseudo ne peut pas être vide." });
                 socket.disconnect(true); // Force déconnexion
                return;
            }

             const isPseudoTaken = Object.values(store.globalPlayers).some(p => p.toLowerCase() === pseudo.toLowerCase());
             if (isPseudoTaken) {
                  console.log(`[game] ⚠️ Pseudo '${pseudo}' déjà pris. Déconnexion ${socket.id}`);
                  socket.emit("joinError", { message: `Le pseudo '${pseudo}' est déjà utilisé.` });
                  socket.disconnect(true);
                  return;
             }

            // Stocker le joueur
            store.globalPlayers[socket.id] = pseudo;
            socket.join(store.GLOBAL_ROOM);
            console.log(`[game] 🔑 Joueur ${pseudo} (${socket.id}) a rejoint la salle: ${store.GLOBAL_ROOM}`);

            // Envoie confirmation, carte et spawn AU NOUVEAU JOUEUR
             console.log(`[Game] Envoi gameJoined à ${pseudo} (${socket.id}) avec carte et spawn`);
             socket.emit("gameJoined", {
                 room: store.GLOBAL_ROOM,
                 id: socket.id,
                 currentMap: store.currentMapJson, // JSON brut de la carte
                 spawnPoint: store.currentSpawnPoint // Point de spawn actuel
              });

             // Mettre à jour la liste des joueurs pour TOUT LE MONDE
             const playersList = Object.entries(store.globalPlayers).map(([id, pseu]) => ({ id, pseudo: pseu }));
             io.in(store.GLOBAL_ROOM).emit("playersList", { players: playersList });
             console.log(`[Game] Liste joueurs mise à jour envoyée. Total: ${playersList.length}`);
        });

        // --- Joueur prêt (après chargement scène client) ---
        socket.on("playerReady", () => {
            const pseudo = store.globalPlayers[socket.id];
            console.log(`[Game] ✅ playerReady reçu de ${pseudo || 'ID inconnu'} (${socket.id}).`);

            if (pseudo) { // S'assurer que le joueur est bien dans notre store

                // <<< MODIFICATION: On informe toujours les autres qu'un joueur spawn >>>
                 const spawnDataForOthers = {
                     id: socket.id,
                     pseudo: pseudo,
                     spawnPoint: store.currentSpawnPoint // Position initiale pour les autres clients
                 };
                 // Note: On ne diffuse pas ici, car le spawn initial est géré par l'horloge.
                 // On va juste informer le nouveau des anciens.
                 // socket.broadcast.to(store.GLOBAL_ROOM).emit("spawnPlayer", spawnDataForOthers); // Retiré, géré par l'horloge
                 // console.log(`[Game] Envoi spawnPlayer (avec spawnPoint) aux autres pour ${pseudo} (${socket.id})`);

                // Informer le nouveau joueur des joueurs DÉJÀ présents
                // <<< MODIFICATION: On envoie la position de spawn actuelle pour les joueurs existants >>>
                // (Même s'ils ne sont peut-être pas VRAIMENT là, le client les créera et ils seront respawnés correctement au prochain tick si besoin)
                const existingPlayers = Object.entries(store.globalPlayers)
                                         .filter(([id, _]) => id !== socket.id) // Exclut soi-même
                                         .map(([id, existingPseudo]) => {
                                             // On n'a plus besoin d'état 'dead'/'alive' ici
                                             return {
                                                 id,
                                                 pseudo: existingPseudo,
                                                 spawnPoint: store.currentSpawnPoint // <<< AJOUT: Envoyer un point de spawn (même si c'est l'actuel)
                                             };
                                         });
                 if (existingPlayers.length > 0) {
                     console.log(`[Game DEBUG] Données pour existingPlayers (emit):`, { players: existingPlayers });
                     console.log(`[Game] Envoi des ${existingPlayers.length} joueurs existants (avec spawnPoint) à ${pseudo} (${socket.id})`);
                     socket.emit("existingPlayers", { players: existingPlayers });
                 } else {
                      console.log(`[Game] Aucun autre joueur existant à envoyer à ${pseudo} (${socket.id})`);
                 }

                 // <<< MODIFICATION: Marquer le joueur comme mort pour que l'horloge gère son premier spawn >>>
                 store.deadPlayers.add(socket.id);
                 console.log(`[Game] Joueur ${pseudo} (${socket.id}) ajouté à deadPlayers pour le respawn initial via l'horloge.`);
                 // On ne diffuse plus 'playerStateUpdate' dead ici

            } else {
                console.warn(`[Game] ⚠️ playerReady reçu mais joueur ${socket.id} inconnu dans globalPlayers.`);
            }
        });

        // --- Mouvement joueur ---
        socket.on("playerMove", (data) => {
             if (data == null || typeof data.x !== 'number' || typeof data.y !== 'number' ||
                 typeof data.isRunning !== 'boolean' || typeof data.isIdle !== 'boolean' ||
                 typeof data.flip !== 'boolean' ||
                 typeof data.velocityX !== 'number' || typeof data.velocityY !== 'number') {
                 console.warn(`[game] ⚠️ Données playerMove invalides reçues de ${socket.id}`);
                 return;
             }

              socket.broadcast.to(store.GLOBAL_ROOM).emit("updatePlayer", {
                  id: socket.id,
                  x: data.x,
                  y: data.y,
                  isRunning: data.isRunning,
                  isIdle: data.isIdle,
                  flip: data.flip,
                  velocityX: data.velocityX,
                  velocityY: data.velocityY
              });
        });

        // --- Demande de liste de joueurs ---
        socket.on("getPlayersList", () => {
             const pseudo = store.globalPlayers[socket.id];
             console.log(`[Game] getPlayersList demandé par ${pseudo || 'ID inconnu'} (${socket.id})`);
             const playersList = Object.entries(store.globalPlayers).map(([id, pseu]) => ({ id, pseudo: pseu }));
              socket.emit("playersList", { players: playersList });
        });

        // --- Événement de mort reçu du client (MODIFIÉ) ---
         socket.on("playerDied", () => {
             const pseudo = store.globalPlayers[socket.id];
             if (pseudo) {
                 // <<< MODIFICATION: On diffuse 'removePlayer' et on ajoute à deadPlayers pour respawn >>>

                 // 1. Informer tout le monde de supprimer ce joueur IMMÉDIATEMENT
                 console.log(`[Game] ☠️ playerDied reçu de ${pseudo} (${socket.id}). Broadcast removePlayer.`);
                 io.in(store.GLOBAL_ROOM).emit('removePlayer', { id: socket.id });

                 // 2. Ajouter à la liste pour le prochain cycle de respawn par l'horloge
                 if (!store.deadPlayers.has(socket.id)) { // Évite ajout multiple si spam
                      store.deadPlayers.add(socket.id);
                      console.log(`[Game] Joueur ${pseudo} (${socket.id}) ajouté à deadPlayers pour prochain respawn.`);
                 } else {
                     console.log(`[Game] ⚠️ Joueur ${pseudo} (${socket.id}) déjà dans deadPlayers lors de playerDied.`);
                 }

                 // On ne diffuse plus 'playerStateUpdate' dead

             } else {
                  console.warn(`[Game] ⚠️ playerDied reçu mais joueur ${socket.id} inconnu dans globalPlayers.`);
             }
         });


        // --- Déconnexion ---
        socket.on("disconnect", (reason) => {
            const pseudo = store.globalPlayers[socket.id]; // Récupérer le pseudo AVANT de supprimer
            console.log(`[game] 🔴 [${socket.id}] Déconnexion. Raison: ${reason}. Joueur: ${pseudo || 'Inconnu'}`);

            if (pseudo) { // Si le joueur était bien dans notre liste
                const wasInDeadList = store.deadPlayers.has(socket.id); // Vérifie s'il attendait un respawn

                delete store.globalPlayers[socket.id]; // Retirer des joueurs actifs
                store.deadPlayers.delete(socket.id); // Retirer des morts au cas où

                // Informer les autres joueurs qu'il faut supprimer ce joueur
                io.in(store.GLOBAL_ROOM).emit("removePlayer", { id: socket.id });

                // Mettre à jour la liste des joueurs pour tout le monde
                const playersList = Object.entries(store.globalPlayers).map(([id, pseu]) => ({ id, pseudo: pseu }));
                io.in(store.GLOBAL_ROOM).emit("playersList", { players: playersList });

                console.log(`[Game] Joueur ${pseudo} (${socket.id}) supprimé des stores. Joueurs restants: ${playersList.length}. Attendait respawn: ${wasInDeadList}`);
            } else {
                 console.log(`[Game] Déconnexion d'un socket (${socket.id}) non trouvé dans globalPlayers.`);
            }
        });
    });
};