
const fileInput = document.getElementById("scheduleFile");
const uploadZone = document.getElementById("uploadZone");
const filePreview = document.getElementById("filePreview");
const fileName = document.getElementById("fileName");
const fileSize = document.getElementById("fileSize");
const fileSymbol = document.getElementById("fileSymbol");
const generateButton = document.getElementById("generateButton");
const uploadError = document.getElementById("uploadError");
const toast = document.getElementById("toastMessage");

let selectedFile = null;
let toastTimeout;

const allowedExtensions = [".png", ".jpg", ".jpeg", ".pdf"];
const maxFileSize = 10 * 1024 * 1024;

document.getElementById("todayLabel").textContent =
    new Intl.DateTimeFormat("tr-TR", {
        day: "numeric", month: "long", year: "numeric"
    }).format(new Date());

function showToast(message) {
    toast.textContent = message;
    toast.classList.add("show");
    clearTimeout(toastTimeout);
    toastTimeout = setTimeout(() => toast.classList.remove("show"), 3500);
}

function showError(message) {
    uploadError.textContent = message;
    uploadError.hidden = false;
}

function clearFile() {
    selectedFile = null;
    fileInput.value = "";
    filePreview.hidden = true;
    uploadZone.hidden = false;
    generateButton.disabled = true;
    uploadError.hidden = true;
}

function handleFile(file) {
    uploadError.hidden = true;

    if (!file) return;

    const extension = "." + file.name.split(".").pop().toLowerCase();

    if (!allowedExtensions.includes(extension)) {
        showError("Lütfen PNG, JPG, JPEG veya PDF formatında bir dosya seç.");
        return;
    }

    if (file.size > maxFileSize) {
        showError("Dosya boyutu en fazla 10 MB olabilir.");
        return;
    }

    if (file.size === 0) {
        showError("Seçtiğin dosya boş görünüyor.");
        return;
    }

    selectedFile = file;
    fileName.textContent = file.name;
    fileSize.textContent = (file.size / 1024 / 1024).toFixed(2) + " MB";
    fileSymbol.textContent = extension === ".pdf" ? "PDF" : "IMG";
    uploadZone.hidden = true;
    filePreview.hidden = false;
    generateButton.disabled = false;
}

fileInput.addEventListener("change", () => {
    handleFile(fileInput.files[0]);
});

document.getElementById("removeFile").addEventListener("click", clearFile);

["dragenter", "dragover"].forEach(eventName => {
    uploadZone.addEventListener(eventName, event => {
        event.preventDefault();
        uploadZone.classList.add("dragging");
    });
});

["dragleave", "drop"].forEach(eventName => {
    uploadZone.addEventListener(eventName, event => {
        event.preventDefault();
        uploadZone.classList.remove("dragging");
    });
});

uploadZone.addEventListener("drop", event => {
    handleFile(event.dataTransfer.files[0]);
});

uploadZone.addEventListener("click", () => fileInput.click());

uploadZone.addEventListener("keydown", event => {
    if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        fileInput.click();
    }
});

uploadZone.tabIndex = 0;
uploadZone.setAttribute("role", "button");
uploadZone.setAttribute("aria-label", "Vardiya çizelgesi dosyası seç");


const dayOrder = [
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
    "Saturday",
    "Sunday"
];

const dayLabels = {
    Monday: "PZT",
    Tuesday: "SAL",
    Wednesday: "ÇAR",
    Thursday: "PER",
    Friday: "CUM",
    Saturday: "CMT",
    Sunday: "PAZ"
};

function parseTime(value) {
    if (typeof value !== "string" || !/^\d{2}:\d{2}$/.test(value)) {
        return null;
    }

    const [hours, minutes] = value.split(":").map(Number);

    if (hours > 23 || minutes > 59) return null;

    return hours * 60 + minutes;
}

function formatTime(minutes) {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;

    return String(hours).padStart(2, "0") + ":" +
        String(mins).padStart(2, "0");
}

function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, character => ({
        "&": "&amp;",
        "<": "&lt;",
        ">": "&gt;",
        '"': "&quot;",
        "'": "&#39;"
    })[character]);
}

function createBreakPlan(employees) {
    const maxContinuousWork = 300;
    const breakDuration = 60;
    const preferredWorkDuration = 240;

    const shiftsByDay = Object.fromEntries(
        dayOrder.map(day => [day, []])
    );

    const breaksByShift = new Map();
    const warnings = [];
    let totalShiftCount = 0;

    employees.forEach(employee => {
        (employee.shifts || []).forEach(shift => {
            totalShiftCount++;

            const day = shift.day;
            const start = parseTime(shift.startTime);
            const end = parseTime(shift.endTime);

            if (!dayOrder.includes(day)) {
                warnings.push(
                    `${employee.name}: Gün bilgisi okunamadı.`
                );
                return;
            }

            if (start === null || end === null || end <= start) {
                warnings.push(
                    `${employee.name} / ${dayLabels[day]}: ` +
                    "Vardiya saatlerini kontrol et."
                );
                return;
            }

            const record = {
                employeeName: employee.name,
                shift,
                start,
                end,
                day
            };

            shiftsByDay[day].push(record);
            breaksByShift.set(shift, []);
        });
    });

    let totalBreakCount = 0;

    for (const day of dayOrder) {
        const dayShifts = shiftsByDay[day];

        // Önce erken başlayan vardiyaları planla.
        dayShifts.sort((a, b) => a.start - b.start);

        const scheduledBreaks = [];

        for (const record of dayShifts) {
            let workSegmentStart = record.start;
            let shiftBreaks = breaksByShift.get(record.shift);

            while (record.end - workSegmentStart > maxContinuousWork) {
                const preferredStart =
                    workSegmentStart + preferredWorkDuration;

                const latestStart = Math.min(
                    workSegmentStart + maxContinuousWork,
                    record.end - breakDuration
                );

                let chosenStart = null;

                // Mola için 15 dakikalık aralıkları dene.
                for (
                    let candidate = preferredStart;
                    candidate <= latestStart;
                    candidate += 15
                ) {
                    const overlaps = scheduledBreaks.some(existing =>
                        candidate < existing.end &&
                        candidate + breakDuration > existing.start
                    );

                    if (!overlaps) {
                        chosenStart = candidate;
                        break;
                    }
                }

                if (chosenStart === null) {
                    warnings.push(
                        `${record.employeeName} / ${dayLabels[day]}: ` +
                        "Çakışma olmadan mola saati bulunamadı."
                    );
                    break;
                }

                const plannedBreak = {
                    start: chosenStart,
                    end: chosenStart + breakDuration
                };

                scheduledBreaks.push(plannedBreak);
                shiftBreaks.push(plannedBreak);
                totalBreakCount++;

                // Moladan sonra çalışma süresi yeniden başlar.
                workSegmentStart = plannedBreak.end;
            }
        }
    }

    return {
        breaksByShift,
        warnings,
        totalBreakCount,
        totalShiftCount
    };
}

function renderSchedule(result) {
    const employees = Array.isArray(result.employees)
        ? result.employees
        : [];

    const plan = createBreakPlan(employees);
    const tbody = document.getElementById("scheduleBody");

    tbody.innerHTML = "";

    if (employees.length === 0) {
        tbody.innerHTML = `
                                                    <tr>
                                                        <td colspan="8">Çizelgede personel bulunamadı.</td>
                                                    </tr>`;
    }

    employees.forEach((employee, index) => {
        const shifts = Array.isArray(employee.shifts)
            ? employee.shifts
            : [];

        const initials = String(employee.name || "?")
            .trim()
            .split(/\s+/)
            .slice(0, 2)
            .map(part => part.charAt(0))
            .join("")
            .toLocaleUpperCase("tr-TR");

        const row = document.createElement("tr");

        let cells = `
                                                    <td>
                                                        <div class="employee-cell">
                                                            <span class="employee-avatar a${(index % 4) + 1}">
                                                                ${escapeHtml(initials)}
                                                            </span>
                                                            <span>
                                                                <strong>${escapeHtml(employee.name || "İsimsiz personel")}</strong>
                                                                <small>Vardiya personeli</small>
                                                            </span>
                                                        </div>
                                                    </td>`;

        dayOrder.forEach(day => {
            const dayShifts = shifts.filter(shift => shift.day === day);

            if (dayShifts.length === 0) {
                cells += `<td><span class="day-off">Vardiya yok</span></td>`;
                return;
            }

            const blocks = dayShifts.map(shift => {
                const start = parseTime(shift.startTime);
                const end = parseTime(shift.endTime);

                if (start === null || end === null || end <= start) {
                    return `
                                                                <div class="shift-stack">
                                                                    <span class="day-off shift-warning">
                                                                        Saat kontrol edilmeli
                                                                    </span>
                                                                </div>`;
                }

                const plannedBreaks =
                    plan.breaksByShift.get(shift) || [];

                const breakHtml = plannedBreaks.map(item => `
                                                            <div class="break-block">
                                                                <b>Mola</b>
                                                                <span>${formatTime(item.start)}–${formatTime(item.end)}</span>
                                                            </div>
                                                        `).join("");

                const needsBreak = end - start > 300;

                const warningHtml = needsBreak && plannedBreaks.length === 0
                    ? `<small class="break-warning">Mola planlanamadı</small>`
                    : "";

                return `
                                                            <div class="shift-stack">
                                                                <div class="shift-block">
                                                                    <b>${formatTime(start)}–${formatTime(end)}</b>
                                                                    <small>Vardiya</small>
                                                                </div>
                                                                ${breakHtml}
                                                                ${warningHtml}
                                                            </div>`;
            }).join("");

            cells += `<td>${blocks}</td>`;
        });

        row.innerHTML = cells;
        tbody.appendChild(row);
    });

    const footer = document.querySelector(".table-footer span:first-child");

    if (footer) {
        footer.textContent = plan.warnings.length > 0
            ? `${plan.totalBreakCount} mola planlandı. ` +
            `${plan.warnings.length} konu kontrol edilmeli.`
            : `${plan.totalBreakCount} mola planlandı. ` +
            "Lütfen planı uygulamadan önce kontrol et.";
    }

    const footerCount = document.querySelector(".table-footer span:last-child");

    if (footerCount) {
        footerCount.textContent = `${employees.length} personel gösteriliyor`;
    }

    return {
        employeeCount: employees.length,
        shiftCount: plan.totalShiftCount,
        breakCount: plan.totalBreakCount,
        warnings: plan.warnings
    };
}


generateButton.addEventListener("click", async () => {
    if (!selectedFile) return;

    const formData = new FormData();
    formData.append("file", selectedFile);

    const token = document.querySelector(
        'input[name="__RequestVerificationToken"]'
    )?.value;

    generateButton.disabled = true;
    generateButton.innerHTML = "<span>◌</span> Çizelge okunuyor...";

    uploadError.hidden = true;

    try {
        const response = await fetch("/Home/ParseSchedule", {
            method: "POST",
            headers: {
                "RequestVerificationToken": token
            },
            body: formData
        });

        const result = await response.json();

        if (!response.ok) {
            throw new Error(
                result.error || "Çizelge okunamadı."
            );
        }

        document.getElementById("parsedJson").textContent =
            JSON.stringify(result, null, 2);

        const resultPanel = document.getElementById("parsedResult");
        resultPanel.hidden = false;


        const summary = renderSchedule(result);

        document.getElementById("employeeCount").textContent =
            summary.employeeCount;

        document.getElementById("shiftCount").textContent =
            summary.shiftCount;

        document.getElementById("breakCount").textContent =
            summary.breakCount;

        document.querySelector(".demo-badge").textContent =
            "OKUNAN ÇİZELGE";

        showToast(
            summary.warnings.length > 0
                ? `Çizelge okundu. ${summary.warnings.length} konu kontrol edilmeli.`
                : "Çizelge okundu ve örnek mola planı oluşturuldu."
        );

        resultPanel.hidden = false;

        document.getElementById("parsedJson").textContent =
            JSON.stringify(result, null, 2);

        document.getElementById("schedule").scrollIntoView({
            behavior: "smooth",
            block: "start"
        });

    } catch (error) {
        showError(error.message || "Beklenmeyen bir hata oluştu.");
    } finally {
        generateButton.disabled = !selectedFile;
        generateButton.innerHTML =
            '<span>✦</span> Çizelgeyi oluştur <span class="button-arrow">→</span>';
    }
});

document.getElementById("copyJsonButton").addEventListener("click", async () => {
    const json = document.getElementById("parsedJson").textContent;

    try {
        await navigator.clipboard.writeText(json);
        showToast("JSON panoya kopyalandı.");
    } catch {
        showToast("Kopyalama başarısız oldu. JSON'u seçerek kopyalayabilirsin.");
    }
});

document.getElementById("printButton").addEventListener("click", () => {
    window.print();
});