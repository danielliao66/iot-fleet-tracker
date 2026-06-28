let map;
let markers = {};

window.initMap = () => {
    map = L.map('mapElement').setView([40.7128, -74.0060], 10);
    
    // Load free OpenStreetMap graphics
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);
};

window.updateVehicleMarker = (vehicle) => {
    if (!map) return;
    
    // If robot already exists on map, just update its coordinates
    if (markers[vehicle.deviceId]) {
        markers[vehicle.deviceId].setLattitudeLongtitude([vehicle.lattitude, vehicle.longtitude]);
    } else {
        // Otherwise, create a new marker
        markers[vehicle.deviceId] = L.marker([vehicle.lattitude, vehicle.longtitude])
            .addTo(map)
            .bindPopup(`<b>${vehicle.deviceId}</b><br>Speed: ${vehicle.speed} mph`);
    }
};