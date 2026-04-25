// Spatial SQL playground interop. Exposes window.spatialSql for the Blazor
// components. The map helpers reuse the same MapLibre bootstrap as the spec
// workspace; the export helper streams a generated file via an anchor tag.
(function () {
    const state = {
        maps: new Map()
    };

    function initializeMap(elementId) {
        const container = document.getElementById(elementId);
        if (!container) {
            return;
        }
        let mapInstance = null;
        if (window.maplibregl && window.maplibregl.Map) {
            try {
                mapInstance = new window.maplibregl.Map({
                    container,
                    style: {
                        version: 8,
                        sources: {},
                        layers: [
                            { id: 'bg', type: 'background', paint: { 'background-color': '#eef3f5' } }
                        ]
                    },
                    center: [-157.8583, 21.3069],
                    zoom: 5
                });
            } catch (err) {
                mapInstance = null;
            }
        }

        const entry = {
            container,
            mapInstance,
            features: [],
            loaded: false
        };

        if (mapInstance) {
            mapInstance.on('load', function () {
                entry.loaded = true;
                renderMaplibre(entry);
            });
        }

        state.maps.set(elementId, entry);
    }

    function setMapFeatures(elementId, features) {
        const entry = state.maps.get(elementId);
        if (!entry) {
            return;
        }
        entry.features = features || [];
        if (entry.mapInstance) {
            renderMaplibre(entry);
        } else {
            renderFallback(entry);
        }
    }

    function renderFallback(entry) {
        if (entry.mapInstance) {
            return;
        }
        entry.container.innerHTML = '';
        const list = document.createElement('ul');
        list.className = 'spec-map-fallback-list';
        (entry.features || []).forEach(function (feat) {
            const li = document.createElement('li');
            li.dataset.featureId = feat.id;
            li.textContent = feat.label || feat.id;
            list.appendChild(li);
        });
        entry.container.appendChild(list);
    }

    function renderMaplibre(entry) {
        if (!entry.mapInstance || !entry.loaded) {
            return;
        }

        const featureCollection = {
            type: 'FeatureCollection',
            features: (entry.features || [])
                .map(function (feature) {
                    let geometry;
                    try {
                        geometry = JSON.parse(feature.geoJson);
                    } catch (err) {
                        return null;
                    }
                    return {
                        type: 'Feature',
                        geometry: geometry,
                        properties: {
                            id: feature.id,
                            label: feature.label
                        }
                    };
                })
                .filter(function (f) { return f !== null; })
        };

        const map = entry.mapInstance;
        const existingSource = map.getSource('sql-results');
        if (existingSource) {
            existingSource.setData(featureCollection);
        } else {
            map.addSource('sql-results', { type: 'geojson', data: featureCollection });
            map.addLayer({
                id: 'sql-results-fill',
                type: 'fill',
                source: 'sql-results',
                filter: ['==', '$type', 'Polygon'],
                paint: {
                    'fill-color': '#006d77',
                    'fill-opacity': 0.32,
                    'fill-outline-color': '#003c43'
                }
            });
            map.addLayer({
                id: 'sql-results-line',
                type: 'line',
                source: 'sql-results',
                filter: ['any', ['==', '$type', 'LineString'], ['==', '$type', 'Polygon']],
                paint: {
                    'line-color': '#003c43',
                    'line-width': 1.5
                }
            });
            map.addLayer({
                id: 'sql-results-point',
                type: 'circle',
                source: 'sql-results',
                filter: ['==', '$type', 'Point'],
                paint: {
                    'circle-radius': 6,
                    'circle-color': '#006d77',
                    'circle-stroke-color': '#ffffff',
                    'circle-stroke-width': 2
                }
            });
        }

        try {
            const bounds = new window.maplibregl.LngLatBounds();
            featureCollection.features.forEach(function (feature) {
                if (!feature.geometry) {
                    return;
                }
                extendBoundsFromGeometry(bounds, feature.geometry);
            });
            if (!bounds.isEmpty()) {
                map.fitBounds(bounds, { padding: 32, maxZoom: 12, duration: 0 });
            }
        } catch (err) {
            // bounds extension is best-effort; ignore malformed geometry rows
        }
    }

    // Walk a GeoJSON geometry by type, extending the bounds with each (lng, lat)
    // pair. Type-aware so 3D coordinates ([lng, lat, elev]) keep elevation out of
    // the bounding-box math instead of mis-pairing it as latitude.
    function extendBoundsFromGeometry(bounds, geometry) {
        if (!geometry || !geometry.coordinates) {
            return;
        }
        const coords = geometry.coordinates;
        switch (geometry.type) {
            case 'Point':
                extendPoint(bounds, coords);
                break;
            case 'MultiPoint':
            case 'LineString':
                coords.forEach(function (p) { extendPoint(bounds, p); });
                break;
            case 'MultiLineString':
            case 'Polygon':
                coords.forEach(function (ring) {
                    ring.forEach(function (p) { extendPoint(bounds, p); });
                });
                break;
            case 'MultiPolygon':
                coords.forEach(function (polygon) {
                    polygon.forEach(function (ring) {
                        ring.forEach(function (p) { extendPoint(bounds, p); });
                    });
                });
                break;
            case 'GeometryCollection':
                if (Array.isArray(geometry.geometries)) {
                    geometry.geometries.forEach(function (g) {
                        extendBoundsFromGeometry(bounds, g);
                    });
                }
                break;
            default:
                // Unknown geometry type — skip rather than throwing.
                break;
        }
    }

    function extendPoint(bounds, point) {
        if (Array.isArray(point) && typeof point[0] === 'number' && typeof point[1] === 'number') {
            bounds.extend([point[0], point[1]]);
        }
    }

    function disposeMap(elementId) {
        const entry = state.maps.get(elementId);
        if (!entry) {
            return;
        }
        if (entry.mapInstance && typeof entry.mapInstance.remove === 'function') {
            entry.mapInstance.remove();
        }
        state.maps.delete(elementId);
    }

    function downloadFile(filename, mimeType, content) {
        const blob = new Blob([content], { type: mimeType });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    }

    function copyToClipboard(text) {
        if (!navigator || !navigator.clipboard) {
            return Promise.reject(new Error('clipboard unavailable'));
        }
        return navigator.clipboard.writeText(text);
    }

    window.spatialSql = {
        initializeMap,
        setMapFeatures,
        disposeMap,
        downloadFile,
        copyToClipboard
    };
})();
