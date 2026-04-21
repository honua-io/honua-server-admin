// Spec workspace interop helpers — splitter drag, MapLibre bootstrap, and
// a lightweight canvas fallback renderer. Exposed as window.specWorkspace.
(function () {
    const state = {
        splitterDotnet: null,
        dragging: null,
        maps: new Map()
    };

    function attachSplitters(dotNetRef) {
        state.splitterDotnet = dotNetRef;
        document.addEventListener('pointermove', onMove);
        document.addEventListener('pointerup', onUp);
    }

    function beginSplitterDrag(splitterId, clientX) {
        state.dragging = {
            id: splitterId,
            startX: clientX,
            containerWidth: window.innerWidth || 1200
        };
    }

    function onMove(e) {
        if (!state.dragging || !state.splitterDotnet) {
            return;
        }
        const delta = (e.clientX - state.dragging.startX) / Math.max(state.dragging.containerWidth, 1);
        state.splitterDotnet.invokeMethodAsync('OnSplitterDrag', state.dragging.id, delta);
        state.dragging.startX = e.clientX;
    }

    function onUp() {
        state.dragging = null;
    }

    function initializeMap(elementId, dotNetRef) {
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
                        layers: [{ id: 'bg', type: 'background', paint: { 'background-color': '#eef3f5' } }]
                    },
                    center: [-157.8583, 21.3069],
                    zoom: 7
                });
            } catch (err) {
                mapInstance = null;
            }
        }

        const entry = {
            container,
            mapInstance,
            dotNetRef,
            features: [],
            loaded: false,
            clickBound: false
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
            return;
        }

        renderFallback(entry);
    }

    function renderFallback(entry) {
        if (entry.mapInstance) {
            return; // real MapLibre instance owns rendering
        }
        entry.container.innerHTML = '';
        const list = document.createElement('ul');
        list.className = 'spec-map-fallback-list';
        (entry.features || []).forEach(function (feat) {
            const item = document.createElement('li');
            item.dataset.featureId = feat.id;
            item.textContent = `${feat.label || feat.id} (${feat.lat.toFixed(3)}, ${feat.lon.toFixed(3)})`;
            item.addEventListener('click', function () {
                if (entry.dotNetRef) {
                    entry.dotNetRef.invokeMethodAsync('FeatureClicked', feat.id);
                }
            });
            list.appendChild(item);
        });
        entry.container.appendChild(list);
    }

    function renderMaplibre(entry) {
        if (!entry.mapInstance || !entry.loaded) {
            return;
        }

        const data = {
            type: 'FeatureCollection',
            features: (entry.features || []).map(function (feature) {
                return {
                    type: 'Feature',
                    geometry: {
                        type: 'Point',
                        coordinates: [feature.lon, feature.lat]
                    },
                    properties: {
                        id: feature.id,
                        label: feature.label || feature.id,
                        source: feature.source
                    }
                };
            })
        };

        const map = entry.mapInstance;
        const existingSource = map.getSource('spec-features');
        if (existingSource) {
            existingSource.setData(data);
        } else {
            map.addSource('spec-features', {
                type: 'geojson',
                data
            });

            map.addLayer({
                id: 'spec-features-fill',
                type: 'circle',
                source: 'spec-features',
                paint: {
                    'circle-radius': 7,
                    'circle-color': '#006d77',
                    'circle-stroke-width': 2,
                    'circle-stroke-color': '#ffffff'
                }
            });

            map.addLayer({
                id: 'spec-features-label',
                type: 'symbol',
                source: 'spec-features',
                layout: {
                    'text-field': ['get', 'label'],
                    'text-size': 11,
                    'text-offset': [0, 1.4]
                },
                paint: {
                    'text-color': '#173042',
                    'text-halo-color': '#ffffff',
                    'text-halo-width': 1
                }
            });
        }

        if (!entry.clickBound) {
            map.on('click', 'spec-features-fill', function (event) {
                const feature = event.features && event.features[0];
                if (!feature || !entry.dotNetRef) {
                    return;
                }

                entry.dotNetRef.invokeMethodAsync('FeatureClicked', feature.properties.id);
            });

            map.on('mouseenter', 'spec-features-fill', function () {
                map.getCanvas().style.cursor = 'pointer';
            });

            map.on('mouseleave', 'spec-features-fill', function () {
                map.getCanvas().style.cursor = '';
            });

            entry.clickBound = true;
        }

        if (data.features.length > 0) {
            const bounds = new window.maplibregl.LngLatBounds();
            data.features.forEach(function (feature) {
                bounds.extend(feature.geometry.coordinates);
            });

            map.fitBounds(bounds, {
                padding: 32,
                maxZoom: 11,
                duration: 0
            });
        }
    }

    function getTextSelection(element) {
        if (!element) {
            return { start: 0, end: 0 };
        }

        return {
            start: element.selectionStart || 0,
            end: element.selectionEnd || 0
        };
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

    window.specWorkspace = {
        attachSplitters,
        beginSplitterDrag,
        getTextSelection,
        initializeMap,
        setMapFeatures,
        disposeMap
    };
})();
