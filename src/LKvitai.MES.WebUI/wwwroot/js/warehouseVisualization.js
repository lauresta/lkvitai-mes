(function () {
    const contexts = {};

    function toHexColor(value) {
        if (!value || typeof value !== "string") {
            return 0x999999;
        }

        const normalized = value.trim().replace("#", "");
        const parsed = Number.parseInt(normalized, 16);
        return Number.isNaN(parsed) ? 0x999999 : parsed;
    }

    function dispose(containerId) {
        const context = contexts[containerId];
        if (!context) {
            return;
        }

        window.removeEventListener("resize", context.onResize);
        context.renderer.domElement.removeEventListener("click", context.onClick);
        context.controls.dispose();
        context.renderer.dispose();
        context.container.innerHTML = "";
        delete contexts[containerId];
    }

    function render(containerId, bins, dotNetRef) {
        dispose(containerId);

        const container = document.getElementById(containerId);
        if (!container || !window.THREE || !window.THREE.OrbitControls) {
            return;
        }

        const scene = new THREE.Scene();
        scene.background = new THREE.Color(0xf8f9fa);

        const width = Math.max(container.clientWidth, 300);
        const height = Math.max(container.clientHeight, 300);

        const camera = new THREE.PerspectiveCamera(60, width / height, 0.1, 2000);
        camera.position.set(40, 40, 40);

        const renderer = new THREE.WebGLRenderer({ antialias: true });
        renderer.setPixelRatio(window.devicePixelRatio || 1);
        renderer.setSize(width, height);
        container.appendChild(renderer.domElement);

        const controls = new THREE.OrbitControls(camera, renderer.domElement);
        controls.enableDamping = true;
        controls.dampingFactor = 0.07;
        controls.target.set(0, 0, 0);
        controls.update();

        scene.add(new THREE.AmbientLight(0xffffff, 0.95));
        const keyLight = new THREE.DirectionalLight(0xffffff, 0.8);
        keyLight.position.set(30, 60, 20);
        scene.add(keyLight);

        const gridHelper = new THREE.GridHelper(240, 40, 0xbfc5cd, 0xe4e7eb);
        scene.add(gridHelper);

        const raycaster = new THREE.Raycaster();
        const mouse = new THREE.Vector2();
        const meshesByCode = {};
        const interactiveMeshes = [];

        (bins || []).forEach((bin) => {
            const geometry = new THREE.BoxGeometry(0.95, 0.95, 0.95);
            const material = new THREE.MeshStandardMaterial({
                color: toHexColor(bin.color),
                metalness: 0.15,
                roughness: 0.55
            });
            const cube = new THREE.Mesh(geometry, material);
            const x = Number(bin.coordinates?.x || 0);
            const y = Number(bin.coordinates?.z || 0);
            const z = Number(bin.coordinates?.y || 0);
            cube.position.set(x, y, z);
            cube.userData = {
                code: bin.code,
                baseColor: toHexColor(bin.color)
            };
            scene.add(cube);
            interactiveMeshes.push(cube);
            meshesByCode[bin.code] = cube;
        });

        let selectedCode = null;
        function applySelection(code) {
            selectedCode = code || null;
            interactiveMeshes.forEach((mesh) => {
                const material = mesh.material;
                if (!material || !material.color) {
                    return;
                }

                if (selectedCode && mesh.userData.code === selectedCode) {
                    material.color.setHex(0xffd166);
                    material.emissive.setHex(0x222200);
                } else {
                    material.color.setHex(mesh.userData.baseColor);
                    material.emissive.setHex(0x000000);
                }
            });
        }

        function focusBin(code) {
            const mesh = meshesByCode[code];
            if (!mesh) {
                return;
            }

            const target = mesh.position;
            controls.target.set(target.x, target.y, target.z);
            camera.position.set(target.x + 12, target.y + 12, target.z + 12);
            controls.update();
            applySelection(code);
        }

        function onClick(event) {
            const bounds = renderer.domElement.getBoundingClientRect();
            mouse.x = ((event.clientX - bounds.left) / bounds.width) * 2 - 1;
            mouse.y = -((event.clientY - bounds.top) / bounds.height) * 2 + 1;

            raycaster.setFromCamera(mouse, camera);
            const intersects = raycaster.intersectObjects(interactiveMeshes);
            if (intersects.length === 0) {
                return;
            }

            const code = intersects[0].object?.userData?.code;
            if (!code) {
                return;
            }

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
        };

        renderer.domElement.addEventListener("click", onClick);
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
            focusBin,
            applySelection,
            stop: () => window.cancelAnimationFrame(animationFrameHandle)
        };
    }

    function focusBin(containerId, code) {
        const context = contexts[containerId];
        if (!context) {
            return;
        }

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

            dispose(containerId);
        }
    };
})();
