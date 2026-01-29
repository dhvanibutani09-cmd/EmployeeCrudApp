class TimeTracker {
    constructor() {
        this.status = 'stopped'; // running, stopped
        this.startTime = null;
        this.elapsedTime = 0;
        this.taskName = '';
        this.dailyTotal = 0;
        this.interval = null;

        this.loadState();
        this.initUI();
        this.startUIUpdater();
    }

    loadState() {
        const state = JSON.parse(localStorage.getItem('tt_state') || '{}');
        const today = new Date().toDateString();
        const savedDate = localStorage.getItem('tt_date');

        if (savedDate !== today) {
            this.dailyTotal = 0;
            localStorage.setItem('tt_date', today);
            localStorage.setItem('tt_daily_total', '0');
        } else {
            this.dailyTotal = parseInt(localStorage.getItem('tt_daily_total') || '0');
        }

        this.status = state.status || 'stopped';
        // Ensure status reflects the absence of 'paused'
        if (this.status === 'paused') this.status = 'stopped';

        this.startTime = state.startTime ? new Date(state.startTime) : null;
        this.taskName = state.taskName || '';

        if (this.status === 'running' && this.startTime) {
            this.elapsedTime = Math.floor((new Date() - this.startTime) / 1000);
        } else {
            this.elapsedTime = 0;
        }
    }

    saveState() {
        const state = {
            status: this.status,
            startTime: this.startTime,
            taskName: this.taskName
        };
        localStorage.setItem('tt_state', JSON.stringify(state));
        localStorage.setItem('tt_daily_total', this.dailyTotal.toString());
    }

    initUI() {
        document.addEventListener('DOMContentLoaded', () => {
            const taskInput = document.getElementById('tt-task-name');
            if (taskInput) {
                taskInput.value = this.taskName;
                taskInput.addEventListener('input', (e) => {
                    this.taskName = e.target.value;
                    this.saveState();
                });
            }
            this.updateUIVisibility();
            this.updateDisplay();
        });
    }

    startUIUpdater() {
        this.interval = setInterval(() => {
            if (this.status === 'running') {
                this.elapsedTime = Math.floor((new Date() - this.startTime) / 1000);
                this.updateDisplay();
                this.updateNavbarTimer();
            }
        }, 1000);
    }

    start() {
        const taskInput = document.getElementById('tt-task-name');
        this.taskName = taskInput ? taskInput.value : '';

        // Strictly capture current system time as start time
        this.startTime = new Date();
        this.status = 'running';
        this.saveState();
        this.updateUIVisibility();
    }

    async stop() {
        if (this.status !== 'stopped') {
            const finalStartTime = this.startTime;
            const finalEndTime = new Date(); // Strictly capture current system time as end time
            const finalElapsedTime = Math.floor((finalEndTime - finalStartTime) / 1000);
            const finalTaskName = this.taskName || 'Unnamed Task';

            // Helper to get local ISO string with offset
            const toLocalISOString = (date) => {
                const tzo = -date.getTimezoneOffset();
                const dif = tzo >= 0 ? '+' : '-';
                const pad = (num) => (num < 10 ? '0' : '') + num;
                return date.getFullYear() +
                    '-' + pad(date.getMonth() + 1) +
                    '-' + pad(date.getDate()) +
                    'T' + pad(date.getHours()) +
                    ':' + pad(date.getMinutes()) +
                    ':' + pad(date.getSeconds()) +
                    dif + pad(Math.floor(Math.abs(tzo) / 60)) +
                    ':' + pad(Math.abs(tzo) % 60);
            };

            this.dailyTotal += finalElapsedTime;
            this.status = 'stopped';
            this.elapsedTime = 0;
            this.startTime = null;
            this.saveState();
            this.updateUIVisibility();
            this.updateDisplay();
            this.updateNavbarTimer();

            // Sync with server
            try {
                await fetch('/TimeTracker/SaveEntry', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        taskName: finalTaskName,
                        startTime: toLocalISOString(finalStartTime),
                        endTime: toLocalISOString(finalEndTime),
                        date: toLocalISOString(finalStartTime), // Record the local date of the session
                        durationInSeconds: finalElapsedTime
                    })
                });
            } catch (err) {
                console.error('Failed to sync time entry:', err);
            }
        }
    }

    updateUIVisibility() {
        const startBtn = document.getElementById('tt-start-btn');
        const pauseBtn = document.getElementById('tt-pause-btn'); // Should be hidden or removed
        const stopBtn = document.getElementById('tt-stop-btn');
        const badge = document.getElementById('timer-badge');

        if (!startBtn) return;

        if (this.status === 'running') {
            startBtn.classList.add('d-none');
            if (pauseBtn) pauseBtn.classList.add('d-none');
            stopBtn.classList.remove('d-none');
            badge.classList.remove('d-none');
        } else {
            startBtn.classList.remove('d-none');
            startBtn.innerHTML = '<i class="bi bi-play-fill me-1"></i> Start';
            if (pauseBtn) pauseBtn.classList.add('d-none');
            stopBtn.classList.add('d-none');
            badge.classList.add('d-none');
        }
    }

    updateDisplay() {
        const display = document.getElementById('tt-display');
        const dailyTotalDisplay = document.getElementById('tt-daily-total');
        if (display) display.innerText = this.formatTime(this.elapsedTime);
        if (dailyTotalDisplay) dailyTotalDisplay.innerText = this.formatTime(this.dailyTotal + (this.status === 'running' ? this.elapsedTime : 0));
    }

    updateNavbarTimer() {
        const navTimer = document.getElementById('nav-running-timer');
        if (navTimer) {
            if (this.status === 'running') {
                navTimer.innerText = this.formatTime(this.elapsedTime);
                navTimer.classList.remove('d-none');
            } else {
                navTimer.classList.add('d-none');
            }
        }
    }

    formatTime(seconds) {
        if (isNaN(seconds) || seconds < 0) seconds = 0;
        const h = Math.floor(seconds / 3600);
        const m = Math.floor((seconds % 3600) / 60);
        const s = seconds % 60;
        return [h, m, s].map(v => v < 10 ? '0' + v : v).join(':');
    }
}

window.timeTracker = new TimeTracker();
