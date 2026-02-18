(function () {
    const contexts = {};
    const debugEnabled = true;

    const VISUAL_CONFIG = {
        fallbackColor: 0x999999,
        borderColor: 0x1f2937,
        borderOpacity: 0.48,
        selectionColor: 0x22d3ee,
        selectionPulseMin: 0.82,
        selectionPulseMax: 1.0,
        selectionGlowPulseMin: 0.42,
        selectionGlowPulseMax: 0.82,
        selectionPulseMs: 1500,
        selectionRingRotationSeconds: 2.9,
        selectionRingRadiusFactor: 1.38,
        selectionRingMinRadius: 1.05,
        selectionRingMaxRadius: 7.2,
        selectionRingGlowPulseMin: 0.22,
        selectionRingGlowPulseMax: 0.4,
        selectionRingFloorOffset: 0.07,
        pinHeightFactor: 0.32,
        pinMinOffset: 0.34,
        pinScaleFactor: 0.95,
        pinBounceIdleMs: 3000,
        pinBounceOneUpMs: 200,
        pinBounceOneDownMs: 190,
        pinBouncePauseMs: 150,
        pinBounceTwoUpMs: 180,
        pinBounceTwoDownMs: 170,
        pinBounceOneHeightFactor: 0.12,
        pinBounceTwoHeightFactor: 0.085,
        reservedPatternColor: "#4338CA",
        reservedOverlayOpacity: 0.42
    };

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
            return VISUAL_CONFIG.fallbackColor;
        }

        const normalized = value.trim().replace("#", "");
        const parsed = Number.parseInt(normalized, 16);
        return Number.isNaN(parsed) ? VISUAL_CONFIG.fallbackColor : parsed;
    }

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function easeInOutSine(value) {
        return -(Math.cos(Math.PI * value) - 1) / 2;
    }

    function easeOutCubic(value) {
        return 1 - Math.pow(1 - value, 3);
    }

    function easeInCubic(value) {
        return value * value * value;
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

    function createReservedHatchTexture() {
        const canvas = document.createElement("canvas");
        canvas.width = 64;
        canvas.height = 64;
        const ctx = canvas.getContext("2d");

        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.strokeStyle = VISUAL_CONFIG.reservedPatternColor;
        ctx.lineWidth = 6;
        ctx.globalAlpha = 1;

        for (let offset = -canvas.width; offset <= canvas.width * 2; offset += 16) {
            ctx.beginPath();
            ctx.moveTo(offset, 0);
            ctx.lineTo(offset - canvas.width, canvas.height);
            ctx.stroke();
        }

        const texture = new THREE.CanvasTexture(canvas);
        texture.wrapS = THREE.RepeatWrapping;
        texture.wrapT = THREE.RepeatWrapping;
        texture.repeat.set(0.65, 0.65);
        texture.needsUpdate = true;
        return texture;
    }

    function createPinTexture() {
        const canvas = document.createElement("canvas");
        canvas.width = 96;
        canvas.height = 128;
        const ctx = canvas.getContext("2d");

        ctx.clearRect(0, 0, canvas.width, canvas.height);

        const centerX = canvas.width / 2;
        const centerY = 46;
        const radius = 30;

        ctx.fillStyle = "#22D3EE";
        ctx.strokeStyle = "#0891B2";
        ctx.lineWidth = 6;

        ctx.beginPath();
        ctx.arc(centerX, centerY, radius, 0, Math.PI * 2);
        ctx.fill();
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(centerX - 16, centerY + 22);
        ctx.lineTo(centerX, canvas.height - 10);
        ctx.lineTo(centerX + 16, centerY + 22);
        ctx.closePath();
        ctx.fill();
        ctx.stroke();

        ctx.fillStyle = "#ECFEFF";
        ctx.beginPath();
        ctx.arc(centerX, centerY, 10, 0, Math.PI * 2);
        ctx.fill();

        const texture = new THREE.CanvasTexture(canvas);
        texture.needsUpdate = true;
        return texture;
    }

    function dispose(containerId) {
        const context = contexts[containerId];
        if (!context) {
            log("dispose: no context", { containerId });
            return;
        }

        if (context.stop) {
            context.stop();
        }

        window.removeEventListener("resize", context.onResize);
        context.renderer.domElement.removeEventListener("click", context.onClick);
        context.renderer.domElement.removeEventListener("wheel", context.onWheel);

        if (context.reservedTexture) {
            context.reservedTexture.dispose();
        }

        if (context.pinTexture) {
            context.pinTexture.dispose();
        }

        context.controls.dispose();
        context.renderer.dispose();
        context.container.innerHTML = "";
        delete contexts[containerId];
        log("dispose: completed", { containerId });
    }

    function render(containerId, bins, dotNetRef, initialSelectedCode) {
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
                capacityVolume: Number(bin.capacity?.volume || 0)
            }));

            const minDistance = computeMinDistance(normalizedBins);

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

            const reservedTexture = createReservedHatchTexture();
            const reservedOverlayMaterial = new THREE.MeshBasicMaterial({
                map: reservedTexture,
                transparent: true,
                opacity: VISUAL_CONFIG.reservedOverlayOpacity,
                depthWrite: false,
                side: THREE.DoubleSide,
                toneMapped: false,
                polygonOffset: true,
                polygonOffsetFactor: -1,
                polygonOffsetUnits: -1
            });

            const pinTexture = createPinTexture();
            const selectionPin = new THREE.Sprite(new THREE.SpriteMaterial({
                map: pinTexture,
                transparent: true,
                depthWrite: false,
                toneMapped: false
            }));
            selectionPin.renderOrder = 6;
            selectionPin.visible = false;
            selectionPin.raycast = () => {};
            scene.add(selectionPin);

            function createDashedRingSegments(innerRadius, outerRadius, dashCount, gapRatio, material) {
                const group = new THREE.Group();
                const fullCircle = Math.PI * 2;
                const step = fullCircle / dashCount;
                const dashLength = step * (1 - gapRatio);

                for (let i = 0; i < dashCount; i += 1) {
                    const dashStart = i * step;
                    const dashGeometry = new THREE.RingGeometry(
                        innerRadius,
                        outerRadius,
                        12,
                        1,
                        dashStart,
                        dashLength);
                    const dash = new THREE.Mesh(dashGeometry, material);
                    dash.rotation.x = -Math.PI / 2;
                    dash.renderOrder = 5;
                    dash.raycast = () => {};
                    group.add(dash);
                }

                return group;
            }

            const selectionRingGroup = new THREE.Group();
            selectionRingGroup.visible = false;
            selectionRingGroup.position.set(0, minY + VISUAL_CONFIG.selectionRingFloorOffset, 0);

            const selectionRingOuterMaterial = new THREE.MeshBasicMaterial({
                color: VISUAL_CONFIG.selectionColor,
                transparent: true,
                opacity: 0.92,
                depthWrite: false,
                toneMapped: false,
                side: THREE.DoubleSide
            });

            const selectionRingInnerMaterial = new THREE.MeshBasicMaterial({
                color: VISUAL_CONFIG.selectionColor,
                transparent: true,
                opacity: 0.74,
                depthWrite: false,
                toneMapped: false,
                side: THREE.DoubleSide
            });

            const selectionRingOuter = createDashedRingSegments(
                1.02,
                1.18,
                28,
                0.42,
                selectionRingOuterMaterial);
            selectionRingOuter.renderOrder = 5;
            selectionRingGroup.add(selectionRingOuter);

            const selectionRingInner = createDashedRingSegments(
                0.84,
                0.96,
                22,
                0.46,
                selectionRingInnerMaterial);
            selectionRingInner.renderOrder = 5;
            selectionRingGroup.add(selectionRingInner);

            const selectionRingGlowMaterial = new THREE.MeshBasicMaterial({
                color: VISUAL_CONFIG.selectionColor,
                transparent: true,
                opacity: VISUAL_CONFIG.selectionRingGlowPulseMin,
                depthWrite: false,
                toneMapped: false,
                side: THREE.DoubleSide
            });
            const selectionRingGlow = new THREE.Mesh(
                new THREE.RingGeometry(0.82, 1.18, 100),
                selectionRingGlowMaterial);
            selectionRingGlow.rotation.x = -Math.PI / 2;
            selectionRingGlow.renderOrder = 4;
            selectionRingGlow.raycast = () => {};
            selectionRingGroup.add(selectionRingGlow);
            scene.add(selectionRingGroup);

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

                const baseBorderMaterial = new THREE.LineBasicMaterial({
                    color: VISUAL_CONFIG.borderColor,
                    transparent: true,
                    opacity: VISUAL_CONFIG.borderOpacity,
                    depthWrite: false,
                    toneMapped: false
                });
                const borderGeometry = new THREE.EdgesGeometry(geometry);
                const baseBorder = new THREE.LineSegments(borderGeometry, baseBorderMaterial);
                baseBorder.renderOrder = 2;
                cube.add(baseBorder);

                const selectionBorderMaterial = new THREE.LineBasicMaterial({
                    color: VISUAL_CONFIG.selectionColor,
                    transparent: true,
                    opacity: VISUAL_CONFIG.selectionPulseMin,
                    depthWrite: false,
                    toneMapped: false
                });
                const selectionBorder = new THREE.LineSegments(borderGeometry, selectionBorderMaterial);
                selectionBorder.renderOrder = 5;
                selectionBorder.visible = false;
                selectionBorder.raycast = () => {};
                cube.add(selectionBorder);

                const selectionBorderGlowMaterial = new THREE.LineBasicMaterial({
                    color: VISUAL_CONFIG.selectionColor,
                    transparent: true,
                    opacity: VISUAL_CONFIG.selectionGlowPulseMin,
                    depthWrite: false,
                    toneMapped: false
                });
                const selectionBorderGlow = new THREE.LineSegments(borderGeometry, selectionBorderGlowMaterial);
                selectionBorderGlow.renderOrder = 4;
                selectionBorderGlow.visible = false;
                selectionBorderGlow.scale.set(1.012, 1.012, 1.012);
                selectionBorderGlow.raycast = () => {};
                cube.add(selectionBorderGlow);

                if (bin.isReserved) {
                    const reservedOverlay = new THREE.Mesh(geometry, reservedOverlayMaterial);
                    reservedOverlay.renderOrder = 3;
                    reservedOverlay.scale.set(1.003, 1.003, 1.003);
                    reservedOverlay.raycast = () => {};
                    cube.add(reservedOverlay);
                }

                cube.position.set(meshX, meshY, meshZ);
                cube.userData = {
                    code: bin.code,
                    baseColor: toHexColor(bin.color),
                    baseBorderMaterial,
                    selectionBorder,
                    selectionBorderMaterial,
                    selectionBorderGlow,
                    selectionBorderGlowMaterial,
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
            let selectedMesh = null;
            let cameraFlightHandle = null;

            function computePinBounceOffset(mesh, timestampMs) {
                const bounceOneHeight = clamp(
                    mesh.userData.height * VISUAL_CONFIG.pinBounceOneHeightFactor,
                    0.09,
                    0.52);
                const bounceTwoHeight = clamp(
                    mesh.userData.height * VISUAL_CONFIG.pinBounceTwoHeightFactor,
                    0.07,
                    0.36);

                const totalCycleMs =
                    VISUAL_CONFIG.pinBounceIdleMs +
                    VISUAL_CONFIG.pinBounceOneUpMs +
                    VISUAL_CONFIG.pinBounceOneDownMs +
                    VISUAL_CONFIG.pinBouncePauseMs +
                    VISUAL_CONFIG.pinBounceTwoUpMs +
                    VISUAL_CONFIG.pinBounceTwoDownMs;

                let elapsed = timestampMs % totalCycleMs;
                if (elapsed < VISUAL_CONFIG.pinBounceIdleMs) {
                    return 0;
                }
                elapsed -= VISUAL_CONFIG.pinBounceIdleMs;

                if (elapsed < VISUAL_CONFIG.pinBounceOneUpMs) {
                    const upProgress = elapsed / VISUAL_CONFIG.pinBounceOneUpMs;
                    return bounceOneHeight * easeOutCubic(upProgress);
                }
                elapsed -= VISUAL_CONFIG.pinBounceOneUpMs;

                if (elapsed < VISUAL_CONFIG.pinBounceOneDownMs) {
                    const downProgress = elapsed / VISUAL_CONFIG.pinBounceOneDownMs;
                    return bounceOneHeight * (1 - easeInCubic(downProgress));
                }
                elapsed -= VISUAL_CONFIG.pinBounceOneDownMs;

                if (elapsed < VISUAL_CONFIG.pinBouncePauseMs) {
                    return 0;
                }
                elapsed -= VISUAL_CONFIG.pinBouncePauseMs;

                if (elapsed < VISUAL_CONFIG.pinBounceTwoUpMs) {
                    const upProgress = elapsed / VISUAL_CONFIG.pinBounceTwoUpMs;
                    return bounceTwoHeight * easeOutCubic(upProgress);
                }
                elapsed -= VISUAL_CONFIG.pinBounceTwoUpMs;

                if (elapsed < VISUAL_CONFIG.pinBounceTwoDownMs) {
                    const downProgress = elapsed / VISUAL_CONFIG.pinBounceTwoDownMs;
                    return bounceTwoHeight * (1 - easeInCubic(downProgress));
                }

                return 0;
            }

            function updateSelectionAnchors(mesh, timestampMs) {
                const topY = mesh.position.y + (mesh.userData.height / 2);
                const pinOffset = Math.max(VISUAL_CONFIG.pinMinOffset, mesh.userData.height * VISUAL_CONFIG.pinHeightFactor);
                const pinScale = clamp(
                    Math.max(mesh.userData.width, mesh.userData.depth, mesh.userData.height) * VISUAL_CONFIG.pinScaleFactor,
                    0.65,
                    2.4);
                const pinBounceOffset = computePinBounceOffset(mesh, timestampMs);

                selectionPin.position.set(mesh.position.x, topY + pinOffset + pinBounceOffset, mesh.position.z);
                selectionPin.scale.set(pinScale * 0.7, pinScale, 1);

                const ringRadius = clamp(
                    Math.max(mesh.userData.width, mesh.userData.depth) * VISUAL_CONFIG.selectionRingRadiusFactor,
                    VISUAL_CONFIG.selectionRingMinRadius,
                    VISUAL_CONFIG.selectionRingMaxRadius);

                const meshBottomY = mesh.position.y - (mesh.userData.height / 2);
                selectionRingGroup.position.set(
                    mesh.position.x,
                    meshBottomY + VISUAL_CONFIG.selectionRingFloorOffset,
                    mesh.position.z);
                selectionRingGroup.scale.set(ringRadius, 1, ringRadius);
            }

            function applySelection(code) {
                selectedCode = code || null;
                selectedMesh = selectedCode ? meshesByCode[selectedCode] || null : null;

                interactiveMeshes.forEach((mesh) => {
                    const isSelected = !!selectedMesh && mesh === selectedMesh;
                    if (mesh.userData.selectionBorder) {
                        mesh.userData.selectionBorder.visible = isSelected;
                    }
                    if (mesh.userData.selectionBorderGlow) {
                        mesh.userData.selectionBorderGlow.visible = isSelected;
                    }
                });

                if (!selectedMesh) {
                    selectionPin.visible = false;
                    selectionRingGroup.visible = false;
                    return;
                }

                selectionPin.visible = true;
                selectionRingGroup.visible = true;
                selectionRingGroup.rotation.y = 0;
                updateSelectionAnchors(selectedMesh, performance.now());
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
                const intersects = raycaster.intersectObjects(interactiveMeshes, false);
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

            if (initialSelectedCode) {
                applySelection(initialSelectedCode);
            }

            const ringRotationSpeed = (2 * Math.PI) / VISUAL_CONFIG.selectionRingRotationSeconds;
            let animationFrameHandle = null;
            let previousFrameTimestamp = performance.now();
            function animate(timestamp) {
                animationFrameHandle = window.requestAnimationFrame(animate);
                const now = typeof timestamp === "number" ? timestamp : performance.now();
                const deltaSeconds = clamp((now - previousFrameTimestamp) / 1000, 0, 0.05);
                previousFrameTimestamp = now;

                if (selectedMesh) {
                    updateSelectionAnchors(selectedMesh, now);

                    const pulsePhase = (now % VISUAL_CONFIG.selectionPulseMs) / VISUAL_CONFIG.selectionPulseMs;
                    const pulse = easeInOutSine(pulsePhase);
                    const pulseOpacity = VISUAL_CONFIG.selectionPulseMin +
                        ((VISUAL_CONFIG.selectionPulseMax - VISUAL_CONFIG.selectionPulseMin) * pulse);
                    const glowOpacity = VISUAL_CONFIG.selectionGlowPulseMin +
                        ((VISUAL_CONFIG.selectionGlowPulseMax - VISUAL_CONFIG.selectionGlowPulseMin) * pulse);

                    selectedMesh.userData.selectionBorderMaterial.opacity = pulseOpacity;
                    selectedMesh.userData.selectionBorderGlowMaterial.opacity = glowOpacity;
                    const borderGlowScale = 1.014 + (0.014 * pulse);
                    selectedMesh.userData.selectionBorderGlow.scale.set(borderGlowScale, borderGlowScale, borderGlowScale);

                    selectionRingOuterMaterial.opacity = 0.78 + (0.18 * pulse);
                    selectionRingInnerMaterial.opacity = 0.58 + (0.16 * (1 - pulse));
                    selectionRingGlowMaterial.opacity = VISUAL_CONFIG.selectionRingGlowPulseMin +
                        ((VISUAL_CONFIG.selectionRingGlowPulseMax - VISUAL_CONFIG.selectionRingGlowPulseMin) * pulse);

                    selectionRingGroup.rotation.y = (selectionRingGroup.rotation.y + (ringRotationSpeed * deltaSeconds)) % (Math.PI * 2);
                }

                controls.update();
                renderer.render(scene, camera);
            }

            animate(previousFrameTimestamp);

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
                reservedTexture,
                pinTexture,
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
            log("dispose(api): requested", { containerId });
            dispose(containerId);
        }
    };
})();
