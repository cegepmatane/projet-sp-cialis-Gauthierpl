// Fichier: Script serveur/game.js
const fs = require('fs');
const path = require('path');
const store = require("./store");
// const logFilePath = path.join(__dirname, 'connections-log.json'); // Log désactivé

// --- Horloge de Respawn ---
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
            store.deadPlayers.clear(); // Vide l'original

            playersToRespawn.forEach(playerId => {
                const pseudo = store.globalPlayers[playerId]; // Récupère le pseudo pour log
                if (pseudo) { // Vérifie si le joueur est toujours dans globalPlayers (donc connecté)
                     console.log(`[Game] Envoi respawnPlayer à ${pseudo} (${playerId}) au point`, store.currentSpawnPoint);
                     // Envoie l'événement uniquement à ce joueur spécifiques
                     io.to(playerId).emit('respawnPlayer', { spawnPoint: store.currentSpawnPoint });
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
                // Envoyer un message d'erreur clair avant de déconnecter
                 socket.emit("joinError", { message: "Le pseudo ne peut pas être vide." });
                 socket.disconnect(true); // Force déconnexion
                return;
            }

            // Vérifie si le pseudo est déjà pris (insensible à la casse pour être plus robuste)
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

             // logConnection(socket.id, pseudo, "join"); // Log désactivé

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
                // Informer les AUTRES joueurs qu'un nouveau joueur est apparu
                // *** MODIFICATION RECOMMANDÉE : Inclure le point de spawn ici ***
                 const spawnDataForOthers = {
                     id: socket.id,
                     pseudo: pseudo,
                     spawnPoint: store.currentSpawnPoint // Position initiale pour les autres clients
                 };
                 // <<< AJOUT DEBUG >>>
                 console.log(`[Game DEBUG] Données pour spawnPlayer (broadcast):`, spawnDataForOthers);
                 // <<< FIN AJOUT DEBUG >>>
                 socket.broadcast.to(store.GLOBAL_ROOM).emit("spawnPlayer", spawnDataForOthers);
                 console.log(`[Game] Envoi spawnPlayer (avec spawnPoint) aux autres pour ${pseudo} (${socket.id})`);

                // Informer le nouveau joueur des joueurs DÉJÀ présents
                const existingPlayers = Object.entries(store.globalPlayers)
                                         .filter(([id, _]) => id !== socket.id) // Exclut soi-même
                                         // Ici aussi, on pourrait envoyer leur position actuelle si on la stockait
                                         .map(([id, existingPseudo]) => ({
                                              id,
                                              pseudo: existingPseudo,
                                              // position: store.playerPositions[id] // Exemple si positions stockées
                                          }));
                 if (existingPlayers.length > 0) {
                     // <<< AJOUT DEBUG >>>
                     console.log(`[Game DEBUG] Données pour existingPlayers (emit):`, { players: existingPlayers });
                     // <<< FIN AJOUT DEBUG >>>
                     console.log(`[Game] Envoi des ${existingPlayers.length} joueurs existants à ${pseudo} (${socket.id})`);
                     socket.emit("existingPlayers", { players: existingPlayers });
                 } else {
                      console.log(`[Game] Aucun autre joueur existant à envoyer à ${pseudo} (${socket.id})`);
                 }

                 // Marquer le joueur comme mort pour qu'il respawn au bon endroit via l'horloge
                 // C'est la clé pour le spawn initial correct
                 store.deadPlayers.add(socket.id);
                 console.log(`[Game] Joueur ${pseudo} (${socket.id}) ajouté à deadPlayers pour le respawn initial.`);

            } else {
                console.warn(`[Game] ⚠️ playerReady reçu mais joueur ${socket.id} inconnu dans globalPlayers.`);
                 // Peut-être déconnecter ce socket ? Il ne devrait pas pouvoir envoyer playerReady sans avoir join.
                 // socket.disconnect(true);
            }
        });

        // --- Mouvement joueur ---
        socket.on("playerMove", (data) => {
            // Validation simple des données reçues
             if (data == null || typeof data.x !== 'number' || typeof data.y !== 'number' ||
                 typeof data.isRunning !== 'boolean' || typeof data.isIdle !== 'boolean' ||
                 typeof data.flip !== 'boolean' ||
                 // <<< AJOUT: Validation pour la vélocité >>>
                 typeof data.velocityX !== 'number' || typeof data.velocityY !== 'number') {
                 console.warn(`[game] ⚠️ Données playerMove invalides reçues de ${socket.id}`);
                 return; // Ignorer les données invalides
             }

             // Diffuser aux autres uniquement (broadcast)
              socket.broadcast.to(store.GLOBAL_ROOM).emit("updatePlayer", {
                  id: socket.id,
                  x: data.x,
                  y: data.y,
                  isRunning: data.isRunning,
                  isIdle: data.isIdle,
                  flip: data.flip,
                  // <<< AJOUT: Relayer la vélocité >>>
                  velocityX: data.velocityX,
                  velocityY: data.velocityY
              });
             // Éviter de logguer chaque mouvement pour ne pas spammer la console
             // console.log(`[Game] playerMove reçu de ${store.globalPlayers[socket.id]} (${socket.id})`);
        });

        // --- Demande de liste de joueurs ---
        socket.on("getPlayersList", () => {
             const pseudo = store.globalPlayers[socket.id];
             console.log(`[Game] getPlayersList demandé par ${pseudo || 'ID inconnu'} (${socket.id})`);
             const playersList = Object.entries(store.globalPlayers).map(([id, pseu]) => ({ id, pseudo: pseu }));
             // On l'envoie juste au demandeur
              socket.emit("playersList", { players: playersList });
        });

        // --- Événement de mort reçu du client ---
         socket.on("playerDied", () => {
             const pseudo = store.globalPlayers[socket.id];
             if (pseudo) {
                 console.log(`[Game] ☠️ playerDied reçu de ${pseudo} (${socket.id}). Ajout à deadPlayers.`);
                 store.deadPlayers.add(socket.id); // Ajoute à la liste pour le prochain cycle de respawn
                 // Optionnel: Informer les autres joueurs de l'état 'mort' ?
                 // io.in(store.GLOBAL_ROOM).emit('playerStateUpdate', { id: socket.id, state: 'dead' });
             } else {
                  console.warn(`[Game] ⚠️ playerDied reçu mais joueur ${socket.id} inconnu dans globalPlayers.`);
             }
         });


        // --- Déconnexion ---
        socket.on("disconnect", (reason) => {
            const pseudo = store.globalPlayers[socket.id]; // Récupérer le pseudo AVANT de supprimer
            console.log(`[game] 🔴 [${socket.id}] Déconnexion. Raison: ${reason}. Joueur: ${pseudo || 'Inconnu'}`);

            if (pseudo) { // Si le joueur était bien dans notre liste
                delete store.globalPlayers[socket.id]; // Retirer des joueurs actifs
                store.deadPlayers.delete(socket.id); // Retirer des morts au cas où il y serait

                // Informer les autres joueurs qu'il faut supprimer ce joueur
                io.in(store.GLOBAL_ROOM).emit("removePlayer", { id: socket.id });

                // Mettre à jour la liste des joueurs pour tout le monde
                const playersList = Object.entries(store.globalPlayers).map(([id, pseu]) => ({ id, pseudo: pseu }));
                io.in(store.GLOBAL_ROOM).emit("playersList", { players: playersList });

                console.log(`[Game] Joueur ${pseudo} (${socket.id}) supprimé des stores. Joueurs restants: ${playersList.length}.`);
                // logConnection(socket.id, pseudo, "leave"); // Log désactivé
            } else {
                 // Ce cas peut arriver si le client se connecte mais ne réussit pas le 'joinGame' avant de se déconnecter
                 console.log(`[Game] Déconnexion d'un socket (${socket.id}) non trouvé dans globalPlayers (n'avait pas rejoint ou déjà supprimé).`);
            }
        });
    });
};