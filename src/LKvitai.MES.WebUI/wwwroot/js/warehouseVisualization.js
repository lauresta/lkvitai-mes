(function () {
    const contexts = {};
    const debugEnabled = true;

    const VISUAL_CONFIG = {
        fallbackColor: 0x999999,
        borderColor: 0x1f2937,
        borderOpacity: 0.48,
        selectionColor: 0x00c8e8,
        selectionEdgeOpacityMin: 0.9,
        selectionEdgeOpacityMax: 1.0,
        selectionGlowCoreOpacity: 0.24,
        selectionGlowCoreScaleBase: 1.014,
        outlineEdgeStrength: 7.2,
        outlineEdgeThickness: 2.8,
        outlineEdgeGlow: 0.9,
        outlinePulsePeriodSeconds: 1.5,
        selectionPulseMs: 1500,
        selectionRingOuterRotationSeconds: 7.0,
        selectionRingInnerRotationSeconds: 12.0,
        selectionRingInnerOffsetFactor: 0.03,
        selectionRingBandThicknessFactor: 0.045,
        selectionRingGapFactor: 0.09,
        selectionRingMinInnerRadius: 0.42,
        selectionRingMaxInnerRadius: 4.2,
        selectionRingMinBandThickness: 0.035,
        selectionRingMaxBandThickness: 0.16,
        selectionRingMinGap: 0.04,
        selectionRingMaxGap: 0.26,
        selectionRingOuterDashGapRatio: 0.72,
        selectionRingInnerDashGapRatio: 0.75,
        selectionRingDashArcLength: 0.14,
        selectionRingGlowPulseMin: 0.0,
        selectionRingGlowPulseMax: 0.0,
        selectionRingFloorOffset: 0.01,
        pinHeightFactor: 0.48,
        pinMinOffset: 0.62,
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

    function computeRootBounds(root, bbox, size, center) {
        root.updateWorldMatrix(true, true);
        if (root.geometry && root.geometry.boundingBox) {
            bbox.copy(root.geometry.boundingBox).applyMatrix4(root.matrixWorld);
        } else if (root.geometry) {
            root.geometry.computeBoundingBox();
            bbox.copy(root.geometry.boundingBox).applyMatrix4(root.matrixWorld);
        } else {
            bbox.setFromObject(root);
        }

        bbox.getSize(size);
        bbox.getCenter(center);
    }

    function markAsOverlay(object3d) {
        object3d.traverse((part) => {
            part.userData = part.userData || {};
            part.userData.isOverlay = true;
            part.raycast = () => null;
        });
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

            const supportsOutlinePass =
                typeof THREE.EffectComposer === "function" &&
                typeof THREE.RenderPass === "function" &&
                typeof THREE.OutlinePass === "function";
            let composer = null;
            let outlinePass = null;
            if (supportsOutlinePass) {
                composer = new THREE.EffectComposer(renderer);
                const renderPass = new THREE.RenderPass(scene, camera);
                composer.addPass(renderPass);

                outlinePass = new THREE.OutlinePass(new THREE.Vector2(width, height), scene, camera);
                outlinePass.edgeStrength = VISUAL_CONFIG.outlineEdgeStrength;
                outlinePass.edgeThickness = VISUAL_CONFIG.outlineEdgeThickness;
                outlinePass.edgeGlow = VISUAL_CONFIG.outlineEdgeGlow;
                outlinePass.pulsePeriod = VISUAL_CONFIG.outlinePulsePeriodSeconds;
                outlinePass.visibleEdgeColor.setHex(VISUAL_CONFIG.selectionColor);
                outlinePass.hiddenEdgeColor.setHex(VISUAL_CONFIG.selectionColor);
                outlinePass.selectedObjects = [];
                composer.addPass(outlinePass);
            }
            log("render: selection outline mode", {
                outlinePassActive: !!outlinePass
            });

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

            const selectionPinMaterial = new THREE.MeshStandardMaterial({
                color: VISUAL_CONFIG.selectionColor,
                roughness: 0.4,
                metalness: 0.08,
                emissive: VISUAL_CONFIG.selectionColor,
                emissiveIntensity: 0.22
            });
            const selectionPinHighlightMaterial = new THREE.MeshStandardMaterial({
                color: 0xeaffff,
                roughness: 0.24,
                metalness: 0.05,
                emissive: 0xaef6ff,
                emissiveIntensity: 0.14
            });
            const selectionPin = new THREE.Group();
            const selectionPinBody = new THREE.Mesh(new THREE.ConeGeometry(1, 1, 20), selectionPinMaterial);
            selectionPinBody.rotation.x = Math.PI;
            selectionPinBody.position.y = 0.5;
            selectionPinBody.renderOrder = 7;
            selectionPin.add(selectionPinBody);

            const selectionPinCollar = new THREE.Mesh(new THREE.TorusGeometry(1, 0.2, 10, 28), selectionPinMaterial);
            selectionPinCollar.rotation.x = Math.PI / 2;
            selectionPinCollar.position.y = 1.0;
            selectionPinCollar.renderOrder = 7;
            selectionPin.add(selectionPinCollar);

            const selectionPinHead = new THREE.Mesh(new THREE.SphereGeometry(1, 20, 20), selectionPinMaterial);
            selectionPinHead.position.y = 1.32;
            selectionPinHead.renderOrder = 7;
            selectionPin.add(selectionPinHead);

            const selectionPinHighlight = new THREE.Mesh(new THREE.SphereGeometry(1, 16, 16), selectionPinHighlightMaterial);
            selectionPinHighlight.position.set(0, 1.56, 0);
            selectionPinHighlight.renderOrder = 8;
            selectionPin.add(selectionPinHighlight);

            selectionPin.visible = false;
            markAsOverlay(selectionPin);
            scene.add(selectionPin);

            const selectionBoundsBox = new THREE.Box3();
            const selectionBoundsSize = new THREE.Vector3();
            const selectionBoundsCenter = new THREE.Vector3();

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
                    markAsOverlay(dash);
                    group.add(dash);
                }

                markAsOverlay(group);
                return group;
            }

            function replaceRingBand(targetGroup, innerRadius, outerRadius, dashCount, gapRatio, material) {
                while (targetGroup.children.length > 0) {
                    const child = targetGroup.children[0];
                    targetGroup.remove(child);
                    if (child.geometry) {
                        child.geometry.dispose();
                    }
                }

                const next = createDashedRingSegments(innerRadius, outerRadius, dashCount, gapRatio, material);
                while (next.children.length > 0) {
                    targetGroup.add(next.children[0]);
                }
            }

            const selectionRingGroup = new THREE.Group();
            selectionRingGroup.visible = false;
            selectionRingGroup.position.set(0, minY + VISUAL_CONFIG.selectionRingFloorOffset, 0);

            const selectionRingOuterMaterial = new THREE.MeshBasicMaterial({
                color: VISUAL_CONFIG.selectionColor,
                transparent: true,
                opacity: 0.38,
                depthWrite: false,
                toneMapped: false,
                side: THREE.DoubleSide
            });

            const selectionRingInnerMaterial = new THREE.MeshBasicMaterial({
                color: VISUAL_CONFIG.selectionColor,
                transparent: true,
                opacity: 0.3,
                depthWrite: false,
                toneMapped: false,
                side: THREE.DoubleSide
            });

            const selectionRingOuter = createDashedRingSegments(
                1.0,
                1.12,
                32,
                0.43,
                selectionRingOuterMaterial);
            selectionRingOuter.renderOrder = 5;
            markAsOverlay(selectionRingOuter);
            selectionRingGroup.add(selectionRingOuter);

            const selectionRingInner = createDashedRingSegments(
                0.84,
                0.95,
                24,
                0.46,
                selectionRingInnerMaterial);
            selectionRingInner.renderOrder = 5;
            markAsOverlay(selectionRingInner);
            selectionRingGroup.add(selectionRingInner);

            const selectionRingGlowMaterial = new THREE.MeshBasicMaterial({
                color: VISUAL_CONFIG.selectionColor,
                transparent: true,
                opacity: VISUAL_CONFIG.selectionRingGlowPulseMin,
                depthWrite: false,
                depthTest: false,
                toneMapped: false,
                side: THREE.DoubleSide
            });
            const selectionRingGlow = new THREE.Mesh(
                new THREE.RingGeometry(0.82, 1.18, 100),
                selectionRingGlowMaterial);
            selectionRingGlow.rotation.x = -Math.PI / 2;
            selectionRingGlow.renderOrder = 4;
            markAsOverlay(selectionRingGlow);
            selectionRingGroup.add(selectionRingGlow);
            markAsOverlay(selectionRingGroup);
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

                const selectionEdgesMaterial = new THREE.LineBasicMaterial({
                    color: VISUAL_CONFIG.selectionColor,
                    transparent: true,
                    opacity: VISUAL_CONFIG.selectionEdgeOpacityMax,
                    depthWrite: false,
                    toneMapped: false
                });
                const selectionEdges = new THREE.LineSegments(borderGeometry, selectionEdgesMaterial);
                selectionEdges.renderOrder = 11;
                selectionEdges.visible = false;
                selectionEdges.scale.set(1.007, 1.007, 1.007);
                markAsOverlay(selectionEdges);
                cube.add(selectionEdges);

                const selectionGlowCoreMaterial = new THREE.MeshBasicMaterial({
                    color: VISUAL_CONFIG.selectionColor,
                    transparent: true,
                    opacity: VISUAL_CONFIG.selectionGlowCoreOpacity,
                    depthWrite: false,
                    depthTest: true,
                    toneMapped: false,
                    side: THREE.BackSide,
                    blending: THREE.NormalBlending,
                    polygonOffset: true,
                    polygonOffsetFactor: -1,
                    polygonOffsetUnits: -1
                });
                const selectionGlowCore = new THREE.Mesh(geometry, selectionGlowCoreMaterial);
                selectionGlowCore.renderOrder = 10;
                selectionGlowCore.visible = false;
                selectionGlowCore.position.set(0, 0, 0);
                selectionGlowCore.scale.set(
                    VISUAL_CONFIG.selectionGlowCoreScaleBase,
                    VISUAL_CONFIG.selectionGlowCoreScaleBase,
                    VISUAL_CONFIG.selectionGlowCoreScaleBase);
                markAsOverlay(selectionGlowCore);
                cube.add(selectionGlowCore);

                if (bin.isReserved) {
                    const reservedOverlay = new THREE.Mesh(geometry, reservedOverlayMaterial);
                    reservedOverlay.renderOrder = 3;
                    reservedOverlay.scale.set(1.003, 1.003, 1.003);
                    markAsOverlay(reservedOverlay);
                    cube.add(reservedOverlay);
                }

                cube.position.set(meshX, meshY, meshZ);
                cube.userData = {
                    code: bin.code,
                    baseColor: toHexColor(bin.color),
                    baseBorderMaterial,
                    selectionEdges,
                    selectionEdgesMaterial,
                    selectionGlowCore,
                    selectionGlowCoreMaterial,
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

            function buildSelectionRingProfile(mesh) {
                computeRootBounds(mesh, selectionBoundsBox, selectionBoundsSize, selectionBoundsCenter);
                const width = selectionBoundsSize.x;
                const depth = selectionBoundsSize.z;
                const diagonal = Math.sqrt((width * width) + (depth * depth));
                const innerRadius = clamp(
                    (diagonal * 0.5) + (diagonal * VISUAL_CONFIG.selectionRingInnerOffsetFactor),
                    VISUAL_CONFIG.selectionRingMinInnerRadius,
                    VISUAL_CONFIG.selectionRingMaxInnerRadius);
                const bandThickness = clamp(
                    diagonal * VISUAL_CONFIG.selectionRingBandThicknessFactor,
                    VISUAL_CONFIG.selectionRingMinBandThickness,
                    VISUAL_CONFIG.selectionRingMaxBandThickness);
                const bandGap = clamp(
                    diagonal * VISUAL_CONFIG.selectionRingGapFactor,
                    VISUAL_CONFIG.selectionRingMinGap,
                    VISUAL_CONFIG.selectionRingMaxGap);

                const innerBandInner = Math.max(0.05, innerRadius);
                const innerBandOuter = innerBandInner + bandThickness;
                const outerBandInner = innerBandOuter + bandGap;
                const outerBandOuter = outerBandInner + bandThickness;
                const glowInner = innerBandInner + (bandThickness * 0.2);
                const glowOuter = outerBandOuter - (bandThickness * 0.2);
                const dashCount = clamp(
                    Math.round(((Math.PI * 2) * outerBandOuter) / VISUAL_CONFIG.selectionRingDashArcLength),
                    18,
                    120);

                return {
                    innerBandInner,
                    innerBandOuter,
                    outerBandInner,
                    outerBandOuter,
                    glowInner,
                    glowOuter,
                    dashCount
                };
            }

            function updateSelectionRingProfile(mesh) {
                const profile = buildSelectionRingProfile(mesh);
                replaceRingBand(
                    selectionRingOuter,
                    profile.outerBandInner,
                    profile.outerBandOuter,
                    profile.dashCount,
                    VISUAL_CONFIG.selectionRingOuterDashGapRatio,
                    selectionRingOuterMaterial);
                replaceRingBand(
                    selectionRingInner,
                    profile.innerBandInner,
                    profile.innerBandOuter,
                    Math.max(14, profile.dashCount - 8),
                    VISUAL_CONFIG.selectionRingInnerDashGapRatio,
                    selectionRingInnerMaterial);

                if (selectionRingGlow.geometry) {
                    selectionRingGlow.geometry.dispose();
                }
                selectionRingGlow.geometry = new THREE.RingGeometry(profile.glowInner, profile.glowOuter, 120);
                selectionRingGlow.rotation.x = -Math.PI / 2;
            }

            function updateSelectionAnchors(mesh, timestampMs) {
                computeRootBounds(mesh, selectionBoundsBox, selectionBoundsSize, selectionBoundsCenter);
                const pinOffset = Math.max(VISUAL_CONFIG.pinMinOffset, selectionBoundsSize.y * VISUAL_CONFIG.pinHeightFactor);
                const pinBounceOffset = computePinBounceOffset(mesh, timestampMs);
                const pinBaseY = selectionBoundsBox.max.y + pinOffset;
                const maxFootprint = Math.max(selectionBoundsSize.x, selectionBoundsSize.z);
                const pinHeadRadius = clamp(maxFootprint * 0.11, 0.12, 0.36);
                const pinConeRadius = clamp(maxFootprint * 0.065, 0.08, 0.24);
                const pinConeHeight = clamp(selectionBoundsSize.y * 0.34, 0.22, 0.92);
                const pinCollarRadius = pinConeRadius * 0.92;
                const pinCollarTube = Math.max(0.026, pinConeRadius * 0.24);
                const pinHighlightRadius = pinHeadRadius * 0.29;

                selectionPinBody.scale.set(pinConeRadius, pinConeHeight, pinConeRadius);
                selectionPinBody.position.y = pinConeHeight * 0.5;
                selectionPinCollar.scale.set(pinCollarRadius, pinCollarTube, pinCollarRadius);
                selectionPinCollar.position.y = pinConeHeight + (pinHeadRadius * 0.04);
                selectionPinHead.scale.set(pinHeadRadius, pinHeadRadius, pinHeadRadius);
                selectionPinHead.position.y = pinConeHeight + (pinHeadRadius * 0.84);
                selectionPinHighlight.scale.set(pinHighlightRadius, pinHighlightRadius, pinHighlightRadius);
                selectionPinHighlight.position.set(
                    0,
                    pinConeHeight + (pinHeadRadius * 1.22),
                    0);

                selectionPin.position.set(
                    selectionBoundsCenter.x,
                    pinBaseY + pinBounceOffset,
                    selectionBoundsCenter.z);
                selectionPin.rotation.set(0, 0, 0);

                selectionRingGroup.position.set(
                    selectionBoundsCenter.x,
                    selectionBoundsBox.min.y + VISUAL_CONFIG.selectionRingFloorOffset,
                    selectionBoundsCenter.z);
            }

            function applySelection(code) {
                selectedCode = code || null;
                selectedMesh = selectedCode ? meshesByCode[selectedCode] || null : null;
                const useOutlinePass = !!outlinePass;

                interactiveMeshes.forEach((mesh) => {
                    const isSelected = !!selectedMesh && mesh === selectedMesh;
                    if (mesh.userData.baseBorderMaterial) {
                        mesh.userData.baseBorderMaterial.color.setHex(VISUAL_CONFIG.borderColor);
                        mesh.userData.baseBorderMaterial.opacity = isSelected && !useOutlinePass
                            ? 0.14
                            : VISUAL_CONFIG.borderOpacity;
                    }
                    if (mesh.userData.selectionEdges) {
                        mesh.userData.selectionEdges.visible = isSelected && !useOutlinePass;
                    }
                    if (mesh.userData.selectionGlowCore) {
                        mesh.userData.selectionGlowCore.visible = isSelected && !useOutlinePass;
                    }
                });
                if (outlinePass) {
                    outlinePass.selectedObjects = selectedMesh ? [selectedMesh] : [];
                }

                if (!selectedMesh) {
                    selectionPin.visible = false;
                    selectionRingGroup.visible = false;
                    return;
                }

                selectionPin.visible = true;
                selectionRingGroup.visible = true;
                selectionRingOuter.rotation.y = 0;
                selectionRingInner.rotation.y = 0;
                selectedMesh.updateWorldMatrix(true, true);
                updateSelectionRingProfile(selectedMesh);
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
                if (composer) {
                    composer.setSize(nextWidth, nextHeight);
                }
                if (outlinePass && typeof outlinePass.setSize === "function") {
                    outlinePass.setSize(nextWidth, nextHeight);
                }
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

            const ringOuterRotationSpeed = (2 * Math.PI) / VISUAL_CONFIG.selectionRingOuterRotationSeconds;
            const ringInnerRotationSpeed = (2 * Math.PI) / VISUAL_CONFIG.selectionRingInnerRotationSeconds;
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
                    if (!outlinePass) {
                        const edgeOpacity = VISUAL_CONFIG.selectionEdgeOpacityMin +
                            ((VISUAL_CONFIG.selectionEdgeOpacityMax - VISUAL_CONFIG.selectionEdgeOpacityMin) * pulse);
                        selectedMesh.userData.selectionEdgesMaterial.opacity = edgeOpacity;
                        selectedMesh.userData.selectionGlowCoreMaterial.opacity = VISUAL_CONFIG.selectionGlowCoreOpacity;
                        const coreScale = VISUAL_CONFIG.selectionGlowCoreScaleBase +
                            (VISUAL_CONFIG.selectionGlowCoreScalePulse * pulse);
                        selectedMesh.userData.selectionGlowCore.scale.set(coreScale, coreScale, coreScale);
                    }

                    selectionRingOuterMaterial.opacity = 0.3 + (0.11 * pulse);
                    selectionRingInnerMaterial.opacity = 0.24 + (0.1 * (1 - pulse));
                    selectionRingGlowMaterial.opacity = VISUAL_CONFIG.selectionRingGlowPulseMin +
                        ((VISUAL_CONFIG.selectionRingGlowPulseMax - VISUAL_CONFIG.selectionRingGlowPulseMin) * pulse);

                    selectionRingOuter.rotation.y = (selectionRingOuter.rotation.y + (ringOuterRotationSpeed * deltaSeconds)) % (Math.PI * 2);
                    selectionRingInner.rotation.y = (selectionRingInner.rotation.y - (ringInnerRotationSpeed * deltaSeconds)) % (Math.PI * 2);
                }

                controls.update();
                if (composer) {
                    composer.render();
                } else {
                    renderer.render(scene, camera);
                }
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
