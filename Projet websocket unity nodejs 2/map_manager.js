// Fichier: Script serveur/map_manager.js
const mysql = require('mysql2/promise');
const store = require('./store');

const dbConfig = {
  host: 'localhost', // <-- METS LE VRAI HOSTNAME HOSTINGER
  user: 'u299951540_JeuMulti',
  password : 'METS_UN_MOT_DE_PASSE_LOCAL_ICI', //jai oublié de changer le mot de passe donc ce string est bien le vrai mot de passe de ma base en local mdr
  //password: 'BB9J1eKI33xSxepLfCotoTgP8vSv4be2dqjyOiscLvqRpJQzxb', // ATTENTION: Stocker les mots de passe de manière plus sécurisée en production
  database: 'u299951540_JeuMulti',
  waitForConnections: true,
  connectionLimit: 10,
  queueLimit: 0
};

let pool;

function initializeDbPool() {
    try {
        pool = mysql.createPool(dbConfig);
        console.log('[MapManager] Pool de connexion DB créé.');
    } catch (error) {
        console.error('[MapManager] Erreur lors de la création du pool DB:', error);
        process.exit(1);
    }
}

async function loadMapsFromDB() {
  // ... (fonction inchangée) ...
   if (!pool) {
    console.error('[MapManager] Le pool DB n\'est pas initialisé.');
    return;
  }
  console.log('[MapManager] Chargement des cartes depuis la DB...');
  try {
    const [rows] = await pool.query('SELECT id, Mapcode FROM Map ORDER BY id ASC');
    if (rows && rows.length > 0) {
      store.availableMaps = rows.map(row => ({
        id: row.id,
        mapCode: row.Mapcode
      }));
      store.currentMapIndex = -1;
      console.log(`[MapManager] ${store.availableMaps.length} cartes chargées.`);
    } else {
      console.warn('[MapManager] Aucune carte trouvée dans la base de données.');
      store.availableMaps = [];
      store.currentMapIndex = -1;
    }
  } catch (error) {
    console.error('[MapManager] Erreur lors du chargement des cartes:', error);
    store.availableMaps = [];
    store.currentMapIndex = -1;
  }
}

// Fonction pour trouver le spawn dans le JSON et mettre à jour le store
function updateCurrentMapData(mapJsonString) {
    store.currentMapJson = mapJsonString; // Stocke le JSON brut
    store.currentSpawnPoint = { x: 0, y: 0, z: 0 }; // Reset par défaut
    try {
        const mapData = JSON.parse(mapJsonString);
        if (mapData && mapData.objects) {
            const spawnObject = mapData.objects.find(obj => obj.prefabId === 'cat_spawn');
            if (spawnObject && spawnObject.position) {
                // Stocke les coordonnées du spawn trouvées
                store.currentSpawnPoint = {
                    x: spawnObject.position.x,
                    y: spawnObject.position.y,
                    z: spawnObject.position.z
                };
                 console.log(`[MapManager] Point de spawn mis à jour:`, store.currentSpawnPoint);
            } else {
                 console.warn("[MapManager] Aucun objet 'cat_spawn' trouvé dans la carte chargée. Utilisation de (0,0,0).");
            }
        }
    } catch (e) {
        console.error("[MapManager] Erreur lors du parsing du JSON pour trouver le spawn:", e);
    }
}


function getNextMapJson() {
  if (!store.availableMaps || store.availableMaps.length === 0) {
    console.warn('[MapManager] Aucune carte disponible pour la rotation.');
    store.currentMapJson = null; // Vide la carte actuelle
    store.currentSpawnPoint = { x: 0, y: 0, z: 0 };
    return null;
  }

  store.currentMapIndex++;
  if (store.currentMapIndex >= store.availableMaps.length) {
    store.currentMapIndex = 0;
  }

  const nextMap = store.availableMaps[store.currentMapIndex];
  console.log(`[MapManager] Prochaine carte sélectionnée: ID ${nextMap.id}`);

  // Met à jour le store avec les infos de la nouvelle carte
  updateCurrentMapData(nextMap.mapCode);

  return nextMap.mapCode; // Renvoie la chaîne JSON
}

async function startMapRotation(io) {
  initializeDbPool();
  await loadMapsFromDB();

  const initialMapJson = getNextMapJson(); // Trouve et met à jour le store
  if (initialMapJson) {
      console.log('[MapManager] Diffusion de la carte initiale.');
      io.in(store.GLOBAL_ROOM).emit('loadMap', initialMapJson);
  }

  // Timer de rotation de carte
  setInterval(() => {
    console.log('[MapManager] Changement de carte déclenché par le timer.');
    const nextMapJson = getNextMapJson(); // Trouve et met à jour le store
    if (nextMapJson) {
      // Avant de charger une nouvelle carte, on "tue" tout le monde pour forcer respawn
      // Cela simplifie la gestion de ceux qui auraient fini juste avant
       Object.keys(store.globalPlayers).forEach(playerId => {
           store.deadPlayers.add(playerId);
       });
       console.log(`[MapManager] Tous les joueurs (${store.deadPlayers.size}) marqués comme morts avant chargement nouvelle carte.`);

      io.in(store.GLOBAL_ROOM).emit('loadMap', nextMapJson);
      console.log('[MapManager] Nouvelle carte diffusée aux clients.');
    } else {
        console.warn('[MapManager] Impossible de charger la prochaine carte, aucune carte disponible.');
    }
  }, 30000); // 30 sec 
}

module.exports = {
  startMapRotation,
  loadMapsFromDB,
};