// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

// Aggiornamento PWA on-demand: la pagina invia { type: 'SKIP_WAITING' } quando l'utente
// clicca "Aggiorna" sul banner; solo allora il SW in waiting si attiva. NESSUNO skipWaiting
// automatico in onInstall: l'attivazione di un UPDATE resta on-demand.
// (clients.claim è in onActivate, per controllare i client al primo caricamento -> offline
// da subito; non rompe l'on-demand perché negli update onActivate gira solo dopo il click.)
self.addEventListener('message', event => {
    if (event.data?.type === 'SKIP_WAITING') self.skipWaiting();
});

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
// I pacchetti dati (data/*.json) restano FUORI dal precache: sono grandi, servono solo a chi apre
// i cataloghi, e cache.addAll è atomico — un loro fetch fallito farebbe fallire l'installazione
// del service worker, con l'app che perde l'offline. Vengono messi in cache alla prima richiesta
// riuscita, dal ramo dedicato in onFetch.
const offlineAssetsExclude = [ /^service-worker\.js$/, /^data\/.*\.json$/ ];
const dataPackagePattern = /\/data\/[^/]+\.json$/;

// Base path derivato DINAMICAMENTE dallo scope del service worker: "/" in locale,
// "/dnd-companion-app/" in produzione (GitHub Pages). Nessun hardcoding e nessun sed
// sul SW: 'self.location' è l'URL dello script (.../service-worker.js), quindi './'
// risolve esattamente la cartella da cui il SW è servito.
const base = new URL('./', self.location).pathname;
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    // Delete unused caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));

    // Prende il controllo dei client già aperti SUBITO dopo l'attivazione, senza richiedere
    // un reload manuale: così al PRIMO caricamento la pagina è controllata dal SW e l'offline
    // funziona da subito (causa #1 risolta). Sicuro col FIX 1: il 'controllerchange' che ne
    // deriva NON ricarica (userTriggeredUpdate è false). Negli update il SW resta in 'waiting'
    // e onActivate (quindi claim) gira solo all'attivazione, cioè dopo il click su "Aggiorna":
    // il flusso on-demand col banner resta intatto.
    await self.clients.claim();
}

async function onFetch(event) {
    let cachedResponse = null;
    if (event.request.method === 'GET') {
        // For all navigation requests, try to serve index.html from cache,
        // unless that request is for an offline resource.
        // If you need some URLs to be server-rendered, edit the following check to exclude those URLs
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);

        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);

        // Cache al primo uso per i pacchetti dati: è ciò che rende il pacchetto consultabile
        // offline dopo averlo aperto una volta (§6 dello spec).
        if (!cachedResponse && dataPackagePattern.test(new URL(event.request.url).pathname)) {
            const response = await fetch(event.request);
            if (response.ok) {
                await cache.put(event.request, response.clone());
            }
            return response;
        }
    }

    return cachedResponse || fetch(event.request);
}
