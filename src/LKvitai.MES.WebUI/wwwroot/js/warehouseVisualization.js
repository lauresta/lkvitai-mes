(function () {
    const contexts = {};
    const debugEnabled = true;

    function log(message, payload) {
        if (!debugEnabled) {
            return;
        }

        if (typeof payload === "undefined") {
            console.log(`[warehouse-3d] ${message}`);
            return;
        }

        console.log(`[warehouse-3d] ${message}`, payload);
    }

    function toHexColor(value) {
        if (!value || typeof value !== "string") {
            return 0x999999;
        }

        const normalized = value.trim().replace("#", "");
        const parsed = Number.parseInt(normalized, 16);
        return Number.isNaN(parsed) ? 0x999999 : parsed;
    }

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function computeMinDistance(points) {
        if (!points || points.length < 2) {
            return Number.POSITIVE_INFINITY;
        }

        let minDistance = Number.POSITIVE_INFINITY;
        for (let i = 0; i < points.length - 1; i += 1) {
            for (let j = i + 1; j < points.length; j += 1) {
                const dx = points[i].x - points[j].x;
                const dy = points[i].y - points[j].y;
                const dz = points[i].z - points[j].z;
                const distance = Math.sqrt((dx * dx) + (dy * dy) + (dz * dz));
                if (distance > 0 && distance < minDistance) {
                    minDistance = distance;
                }
            }
        }

        return minDistance;
    }

    function dispose(containerId) {
        const context = contexts[containerId];
        if (!context) {
            log("dispose: no context", { containerId });
            return;
        }

        window.removeEventListener("resize", context.onResize);
        context.renderer.domElement.removeEventListener("click", context.onClick);
        context.renderer.domElement.removeEventListener("wheel", context.onWheel);
        context.controls.dispose();
        context.renderer.dispose();
        context.container.innerHTML = "";
        delete contexts[containerId];
        log("dispose: completed", { containerId });
    }

    function render(containerId, bins, dotNetRef) {
        try {
            dispose(containerId);

            const container = document.getElementById(containerId);
            if (!container || !window.THREE || !window.THREE.OrbitControls) {
                log("render: prerequisites missing", {
                    containerId,
                    hasContainer: !!container,
                    hasThree: !!window.THREE,
                    hasOrbitControls: !!(window.THREE && window.THREE.OrbitControls)
                });
                return;
            }

            log("render: start", {
                containerId,
                binCount: Array.isArray(bins) ? bins.length : 0,
                binsSample: Array.isArray(bins)
                    ? bins.slice(0, 5).map((x) => x.code)
                    : []
            });

            const scene = new THREE.Scene();
            scene.background = new THREE.Color(0xf8f9fa);

            const width = Math.max(container.clientWidth, 300);
            const height = Math.max(container.clientHeight, 300);

            const camera = new THREE.PerspectiveCamera(60, width / height, 0.1, 2000);
            camera.position.set(40, 40, 40);

            const renderer = new THREE.WebGLRenderer({ antialias: true });
            renderer.setPixelRatio(window.devicePixelRatio || 1);
            renderer.setSize(width, height);
            renderer.domElement.style.touchAction = "none";
            container.appendChild(renderer.domElement);

            const controls = new THREE.OrbitControls(camera, renderer.domElement);
            controls.enableRotate = true;
            controls.enableZoom = true;
            controls.enablePan = true;
            controls.enableDamping = true;
            controls.dampingFactor = 0.07;
            controls.zoomSpeed = 1.1;
            controls.rotateSpeed = 0.9;
            controls.panSpeed = 0.9;
            controls.minDistance = 1;
            controls.maxDistance = 5000;
            controls.screenSpacePanning = true;
            controls.mouseButtons = {
                LEFT: THREE.MOUSE.ROTATE,
                MIDDLE: THREE.MOUSE.DOLLY,
                RIGHT: THREE.MOUSE.PAN
            };
            controls.target.set(0, 0, 0);

            scene.add(new THREE.AmbientLight(0xffffff, 0.95));
            const keyLight = new THREE.DirectionalLight(0xffffff, 0.8);
            keyLight.position.set(30, 60, 20);
            scene.add(keyLight);

            const normalizedBins = (bins || []).map((bin) => ({
                bin,
                x: Number(bin.coordinates?.x || 0),
                y: Number(bin.coordinates?.z || 0),
                z: Number(bin.coordinates?.y || 0),
                width: Number(bin.dimensions?.width || 0),
                length: Number(bin.dimensions?.length || 0),
                height: Number(bin.dimensions?.height || 0),
                capacityVolume: Number(bin.capacity?.volume || 0),
                capacityWeight: Number(bin.capacity?.weight || 0)
            }));

            const minDistance = computeMinDistance(normalizedBins);

            // Base footprint is limited by nearest-neighbor distance to avoid overlap.
            const overlapSafeFootprint = Number.isFinite(minDistance)
                ? clamp(minDistance * 0.45, 0.28, 1.4)
                : 0.9;
            const pointsX = normalizedBins.map((x) => x.x);
            const pointsY = normalizedBins.map((x) => x.y);
            const pointsZ = normalizedBins.map((x) => x.z);
            const pointsSpan = Math.max(
                (pointsX.length ? Math.max(...pointsX) - Math.min(...pointsX) : 0),
                (pointsY.length ? Math.max(...pointsY) - Math.min(...pointsY) : 0),
                (pointsZ.length ? Math.max(...pointsZ) - Math.min(...pointsZ) : 0),
                1);
            const baseFootprint = pointsSpan <= 1
                ? Math.min(overlapSafeFootprint, 0.8)
                : pointsSpan < 4
                    ? Math.min(overlapSafeFootprint, 0.9)
                    : Math.min(overlapSafeFootprint, 1.0);

            const volumeValues = normalizedBins
                .map((x) => x.capacityVolume)
                .filter((x) => Number.isFinite(x) && x > 0);
            const maxVolume = volumeValues.length > 0 ? Math.max(...volumeValues) : 0;

            const resolvedBins = normalizedBins.map(({ bin, x, y, z, width, length, height, capacityVolume }) => {
                const hasExplicitDimensions =
                    Number.isFinite(width) && width > 0 &&
                    Number.isFinite(length) && length > 0 &&
                    Number.isFinite(height) && height > 0;

                if (hasExplicitDimensions) {
                    return {
                        bin,
                        width,
                        depth: length,
                        height,
                        centerX: x + (width / 2),
                        centerY: y + (height / 2),
                        centerZ: z + (length / 2),
                        hasExplicitDimensions: true,
                        capacityVolume
                    };
                }

                // Fallback for locations where dimensions are still not provided.
                const footprint = baseFootprint;
                const volumeRatio = maxVolume > 0 && capacityVolume > 0
                    ? clamp(capacityVolume / maxVolume, 0.2, 1)
                    : 0.5;
                const fallbackHeight = clamp(
                    footprint * (0.7 + (1.8 * volumeRatio)),
                    0.25,
                    2.4);

                return {
                    bin,
                    width: footprint,
                    depth: footprint,
                    height: fallbackHeight,
                    centerX: x,
                    centerY: y + (fallbackHeight / 2),
                    centerZ: z,
                    hasExplicitDimensions: false,
                    capacityVolume
                };
            });

            const minX = resolvedBins.length
                ? Math.min(...resolvedBins.map((x) => x.centerX - (x.width / 2)))
                : 0;
            const minY = resolvedBins.length
                ? Math.min(...resolvedBins.map((x) => x.centerY - (x.height / 2)))
                : 0;
            const minZ = resolvedBins.length
                ? Math.min(...resolvedBins.map((x) => x.centerZ - (x.depth / 2)))
                : 0;
            const maxX = resolvedBins.length
                ? Math.max(...resolvedBins.map((x) => x.centerX + (x.width / 2)))
                : 1;
            const maxY = resolvedBins.length
                ? Math.max(...resolvedBins.map((x) => x.centerY + (x.height / 2)))
                : 1;
            const maxZ = resolvedBins.length
                ? Math.max(...resolvedBins.map((x) => x.centerZ + (x.depth / 2)))
                : 1;
            const centerX = (minX + maxX) / 2;
            const centerY = (minY + maxY) / 2;
            const centerZ = (minZ + maxZ) / 2;
            const maxSpan = Math.max(maxX - minX, maxY - minY, maxZ - minZ, 1);
            const cameraDistance = Math.max(12, maxSpan * 2.4);
            const explicitDimensionCount = resolvedBins.filter((x) => x.hasExplicitDimensions).length;

            log("render: scene extents computed", {
                minX, minY, minZ, maxX, maxY, maxZ, centerX, centerY, centerZ, maxSpan, minDistance,
                cameraDistance, baseFootprint, maxVolume, explicitDimensionCount
            });

            controls.target.set(centerX, centerY, centerZ);
            camera.position.set(
                centerX + cameraDistance,
                centerY + cameraDistance * 0.9,
                centerZ + cameraDistance);
            controls.update();

            const gridSize = Math.max(30, Math.ceil(maxSpan * 4));
            const gridDivisions = Math.max(10, Math.min(80, Math.round(gridSize / 2)));
            const gridHelper = new THREE.GridHelper(gridSize, gridDivisions, 0xbfc5cd, 0xe4e7eb);
            scene.add(gridHelper);

            const raycaster = new THREE.Raycaster();
            const mouse = new THREE.Vector2();
            const meshesByCode = {};
            const interactiveMeshes = [];
            const defaultBorderColor = 0x1f2937;
            const selectedBorderColor = 0xffffff;

            resolvedBins.forEach(({ bin, width, depth, height, centerX: meshX, centerY: meshY, centerZ: meshZ, hasExplicitDimensions, capacityVolume }) => {
                const geometry = new THREE.BoxGeometry(width, height, depth);
                const material = new THREE.MeshStandardMaterial({
                    color: toHexColor(bin.color),
                    metalness: 0.15,
                    roughness: 0.55,
                    polygonOffset: true,
                    polygonOffsetFactor: 1,
                    polygonOffsetUnits: 1
                });
                const cube = new THREE.Mesh(geometry, material);
                const borderGeometry = new THREE.EdgesGeometry(geometry);
                const borderMaterial = new THREE.LineBasicMaterial({
                    color: defaultBorderColor,
                    transparent: true,
                    opacity: 0.9,
                    depthWrite: false,
                    toneMapped: false
                });
                const border = new THREE.LineSegments(borderGeometry, borderMaterial);
                border.renderOrder = 2;
                cube.add(border);
                cube.position.set(meshX, meshY, meshZ);
                cube.userData = {
                    code: bin.code,
                    baseColor: toHexColor(bin.color),
                    borderMaterial,
                    width,
                    depth,
                    height,
                    hasExplicitDimensions,
                    capacityVolume
                };
                scene.add(cube);
                interactiveMeshes.push(cube);
                meshesByCode[bin.code] = cube;
            });

            log("render: meshes created", {
                interactiveMeshCount: interactiveMeshes.length,
                cameraPosition: { x: camera.position.x, y: camera.position.y, z: camera.position.z },
                target: { x: controls.target.x, y: controls.target.y, z: controls.target.z },
                sample: interactiveMeshes.slice(0, 5).map((x) => ({
                    code: x.userData.code,
                    width: x.userData.width,
                    depth: x.userData.depth,
                    height: x.userData.height,
                    hasExplicitDimensions: x.userData.hasExplicitDimensions,
                    capacityVolume: x.userData.capacityVolume
                }))
            });

            let selectedCode = null;
            let cameraFlightHandle = null;
            function applySelection(code) {
                selectedCode = code || null;
                interactiveMeshes.forEach((mesh) => {
                    const material = mesh.material;
                    if (!material || !material.color) {
                        return;
                    }

                    if (selectedCode && mesh.userData.code === selectedCode) {
                        material.color.setHex(0xffd700);
                        material.emissive.setHex(0x222200);
                        mesh.userData.borderMaterial?.color?.setHex(selectedBorderColor);
                    } else {
                        material.color.setHex(mesh.userData.baseColor);
                        material.emissive.setHex(0x000000);
                        mesh.userData.borderMaterial?.color?.setHex(defaultBorderColor);
                    }
                });
            }

            function focusBin(code) {
                const mesh = meshesByCode[code];
                if (!mesh) {
                    log("focusBin: code not found", { code });
                    return;
                }

                const target = mesh.position;
                const focusDistance = Math.max(8, cameraDistance * 0.45);
                const desiredTarget = new THREE.Vector3(target.x, target.y, target.z);
                const desiredCamera = new THREE.Vector3(
                    target.x + focusDistance,
                    target.y + focusDistance * 0.85,
                    target.z + focusDistance);

                const startTarget = controls.target.clone();
                const startCamera = camera.position.clone();
                const durationMs = 1000;
                const startedAt = performance.now();

                if (cameraFlightHandle) {
                    window.cancelAnimationFrame(cameraFlightHandle);
                    cameraFlightHandle = null;
                }

                const animateFlight = (timestamp) => {
                    const elapsed = timestamp - startedAt;
                    const progress = Math.min(1, elapsed / durationMs);
                    const eased = progress < 0.5
                        ? 2 * progress * progress
                        : 1 - Math.pow(-2 * progress + 2, 2) / 2;

                    camera.position.lerpVectors(startCamera, desiredCamera, eased);
                    controls.target.lerpVectors(startTarget, desiredTarget, eased);
                    controls.update();

                    if (progress < 1) {
                        cameraFlightHandle = window.requestAnimationFrame(animateFlight);
                    } else {
                        cameraFlightHandle = null;
                    }
                };

                cameraFlightHandle = window.requestAnimationFrame(animateFlight);
                applySelection(code);
                log("focusBin: success", {
                    code,
                    target: { x: desiredTarget.x, y: desiredTarget.y, z: desiredTarget.z },
                    camera: { x: desiredCamera.x, y: desiredCamera.y, z: desiredCamera.z },
                    durationMs
                });
            }

            function onClick(event) {
                const bounds = renderer.domElement.getBoundingClientRect();
                mouse.x = ((event.clientX - bounds.left) / bounds.width) * 2 - 1;
                mouse.y = -((event.clientY - bounds.top) / bounds.height) * 2 + 1;

                raycaster.setFromCamera(mouse, camera);
                const intersects = raycaster.intersectObjects(interactiveMeshes);
                if (intersects.length === 0) {
                    log("click: no intersection");
                    return;
                }

                const code = intersects[0].object?.userData?.code;
                if (!code) {
                    log("click: intersection without code");
                    return;
                }

                log("click: selected", { code });
                applySelection(code);
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync("OnBinSelectedFromJs", code);
                }
            }

            const onResize = () => {
                const nextWidth = Math.max(container.clientWidth, 300);
                const nextHeight = Math.max(container.clientHeight, 300);
                camera.aspect = nextWidth / nextHeight;
                camera.updateProjectionMatrix();
                renderer.setSize(nextWidth, nextHeight);
                log("resize", { nextWidth, nextHeight });
            };

            const onWheel = (event) => {
                log("wheel: zoom intent", {
                    deltaY: event.deltaY,
                    cameraDistanceToTarget: camera.position.distanceTo(controls.target)
                });
            };

            controls.addEventListener("start", () => {
                renderer.domElement.style.cursor = "grabbing";
                log("controls:start", {
                    camera: { x: camera.position.x, y: camera.position.y, z: camera.position.z },
                    target: { x: controls.target.x, y: controls.target.y, z: controls.target.z }
                });
            });

            controls.addEventListener("end", () => {
                renderer.domElement.style.cursor = "grab";
                log("controls:end", {
                    camera: { x: camera.position.x, y: camera.position.y, z: camera.position.z },
                    target: { x: controls.target.x, y: controls.target.y, z: controls.target.z },
                    cameraDistanceToTarget: camera.position.distanceTo(controls.target)
                });
            });

            renderer.domElement.style.cursor = "grab";
            renderer.domElement.addEventListener("click", onClick);
            renderer.domElement.addEventListener("wheel", onWheel, { passive: true });
            window.addEventListener("resize", onResize);

            let animationFrameHandle = null;
            function animate() {
                animationFrameHandle = window.requestAnimationFrame(animate);
                controls.update();
                renderer.render(scene, camera);
            }

            animate();

            contexts[containerId] = {
                container,
                scene,
                camera,
                renderer,
                controls,
                onResize,
                onClick,
                onWheel,
                focusBin,
                applySelection,
                stop: () => {
                    if (animationFrameHandle) {
                        window.cancelAnimationFrame(animationFrameHandle);
                    }
                    if (cameraFlightHandle) {
                        window.cancelAnimationFrame(cameraFlightHandle);
                    }
                }
            };

            log("render: completed", { containerId });
        } catch (error) {
            console.error("[warehouse-3d] render error", error);
        }
    }

    function focusBin(containerId, code) {
        const context = contexts[containerId];
        if (!context) {
            log("focusBin(api): context not found", { containerId, code });
            return;
        }

        log("focusBin(api): requested", { containerId, code });
        context.focusBin(code);
    }

    window.warehouseVisualization = {
        render,
        focusBin,
        dispose: (containerId) => {
            const context = contexts[containerId];
            if (context && context.stop) {
                context.stop();
            }

            log("dispose(api): requested", { containerId });
            dispose(containerId);
        }
    };
})();
