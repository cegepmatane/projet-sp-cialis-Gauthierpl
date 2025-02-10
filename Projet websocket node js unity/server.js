const WebSocket = require('ws');
const wss = new WebSocket.Server({ port: 8080 });

let rooms = {}; // Stocke les salons sous forme { nom_salon: [clients] }
let clients = new Map(); // Associe chaque client WebSocket à un ID unique

console.log("Serveur WebSocket en écoute sur ws://localhost:8080");

wss.on('connection', function connection(ws) {
    console.log("Un joueur est connecté");

    // Génération d'un ID unique pour chaque client
    const clientId = Date.now().toString() + Math.random().toString(36).substr(2, 9);
    clients.set(ws, clientId);

    // Vérification de la connexion toutes les 5 secondes
    setInterval(() => {
        console.log(`Vérification connexion WebSocket - Clients connectés : ${wss.clients.size}`);
    }, 5000);

    ws.on('message', function incoming(message) {
        console.log("Message reçu brut du client :", message);

        if (!message || message.length === 0) {
            console.log("Message vide reçu, ignoré.");
            return;
        }

        try {
            const msg = JSON.parse(message);
            console.log("Message JSON reçu :", msg);

            if (!msg.type) {
                console.log("Message sans type détecté, ignoré.");
                return;
            }

            // Création d'un salon
            if (msg.type === 'createRoom') {
                console.log(`Tentative de création du salon : ${msg.roomName}`);
                if (!rooms[msg.roomName]) {
                    rooms[msg.roomName] = [];
                    console.log(`Salon "${msg.roomName}" créé avec succès.`);
                }
                
                rooms[msg.roomName].push(clientId);
                ws.send(JSON.stringify({ type: 'createRoom', success: true, roomName: msg.roomName }));
                broadcastRooms();
            }

            // Rejoindre un salon
            if (msg.type === 'joinRoom') {
                console.log(`Tentative de rejoindre le salon : ${msg.roomName}`);
                if (rooms[msg.roomName]) {
                    rooms[msg.roomName].push(clientId);
                    console.log(`Un joueur a rejoint le salon "${msg.roomName}".`);
                    ws.send(JSON.stringify({ type: 'joinRoom', success: true, roomName: msg.roomName }));
                    broadcastRooms();
                } else {
                    console.log(`Erreur : Salon "${msg.roomName}" introuvable.`);
                    ws.send(JSON.stringify({ type: 'joinRoom', success: false, error: 'Salon introuvable' }));
                }
            }

            // Un client demande la liste des salons
            if (msg.type === 'getRooms') {
                console.log("Demande de mise à jour des salons reçue.");
                ws.send(JSON.stringify({ type: "updateRooms", rooms: Object.keys(rooms) }));
            }

        } catch (error) {
            console.log("Erreur lors du parsing JSON :", error);
        }
    });

    ws.on('close', () => {
        console.log("Un joueur s'est déconnecté.");
        
        const clientId = clients.get(ws);
        clients.delete(ws);

        // Retirer le joueur des salons
        Object.keys(rooms).forEach(roomName => {
            rooms[roomName] = rooms[roomName].filter(id => id !== clientId);
            if (rooms[roomName].length === 0) {
                console.log(`Le salon "${roomName}" est vide et sera supprimé.`);
                delete rooms[roomName];
            }
        });

        broadcastRooms();
    });
});

function broadcastRooms() {
    const roomsList = Object.keys(rooms);
    console.log("Mise à jour des salons envoyée à tous les clients :", roomsList);

    let count = 0;
    wss.clients.forEach(client => {
        if (client.readyState === WebSocket.OPEN) {
            console.log("Envoi de updateRooms au client...");
            client.send(JSON.stringify({ type: "updateRooms", rooms: roomsList }));
            count++;
        }
    });

    console.log(`Nombre total de clients mis à jour : ${count}`);
}
