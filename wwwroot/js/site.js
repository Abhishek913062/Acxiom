/* AcxiomCRM shared interaction helpers. */
(function () {
    "use strict";

    const body = document.body;
    const sidebar = document.getElementById("crmSidebar");
    const sidebarToggle = document.querySelector("[data-sidebar-toggle]");
    const sidebarCloseButtons = document.querySelectorAll("[data-sidebar-close]");

    function closeSidebar() {
        body.classList.remove("sidebar-open");
        sidebarToggle?.setAttribute("aria-expanded", "false");
    }

    function openSidebar() {
        body.classList.add("sidebar-open");
        sidebarToggle?.setAttribute("aria-expanded", "true");
    }

    sidebarToggle?.addEventListener("click", () => {
        body.classList.contains("sidebar-open") ? closeSidebar() : openSidebar();
    });

    sidebarCloseButtons.forEach((button) => button.addEventListener("click", closeSidebar));

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") closeSidebar();
    });

    // Highlight the matching navigation item without requiring every view to set an active class.
    const currentPath = window.location.pathname.toLowerCase().replace(/\/$/, "");
    document.querySelectorAll("[data-nav-link]").forEach((link) => {
        const linkPath = new URL(link.href, window.location.origin).pathname.toLowerCase().replace(/\/$/, "");
        if (linkPath && linkPath !== "/" && (currentPath === linkPath || currentPath.startsWith(`${linkPath}/`))) {
            link.classList.add("active");
            link.setAttribute("aria-current", "page");
        }
        link.addEventListener("click", () => {
            if (window.matchMedia("(max-width: 991.98px)").matches) closeSidebar();
        });
    });

    // Let views opt in to a lightweight, accessible password visibility control.
    document.querySelectorAll("[data-password-toggle]").forEach((button) => {
        button.addEventListener("click", () => {
            const input = document.querySelector(button.getAttribute("data-password-toggle"));
            if (!input) return;
            const revealed = input.type === "text";
            input.type = revealed ? "password" : "text";
            button.setAttribute("aria-label", revealed ? "Show password" : "Hide password");
            button.setAttribute("aria-pressed", String(!revealed));
            const icon = button.querySelector("i");
            if (icon) icon.className = revealed ? "bi bi-eye" : "bi bi-eye-slash";
        });
    });

    document.querySelectorAll("[data-confirm]").forEach((element) => {
        element.addEventListener("click", (event) => {
            if (!window.confirm(element.getAttribute("data-confirm") || "Are you sure you want to continue?")) {
                event.preventDefault();
            }
        });
    });

    document.querySelectorAll("[data-auto-dismiss]").forEach((alert) => {
        window.setTimeout(() => {
            const instance = window.bootstrap?.Alert.getOrCreateInstance(alert);
            instance?.close();
        }, 5000);
    });

    // Available to any page that needs a small non-blocking notification after an AJAX action.
    window.crmNotify = function (message, type = "success") {
        let region = document.querySelector(".crm-toast-region");
        if (!region) {
            region = document.createElement("div");
            region.className = "toast-container position-fixed top-0 end-0 p-3 crm-toast-region";
            region.setAttribute("aria-live", "polite");
            body.appendChild(region);
        }

        const safeTypes = new Set(["primary", "success", "danger", "warning", "info", "secondary", "dark"]);
        const toast = document.createElement("div");
        toast.className = `toast align-items-center text-bg-${safeTypes.has(type) ? type : "primary"} border-0`;
        toast.setAttribute("role", "status");

        const row = document.createElement("div");
        row.className = "d-flex";
        const toastBody = document.createElement("div");
        toastBody.className = "toast-body";
        toastBody.textContent = String(message);
        const closeButton = document.createElement("button");
        closeButton.type = "button";
        closeButton.className = "btn-close btn-close-white me-2 m-auto";
        closeButton.dataset.bsDismiss = "toast";
        closeButton.setAttribute("aria-label", "Close");
        row.append(toastBody, closeButton);
        toast.appendChild(row);
        region.appendChild(toast);
        const instance = window.bootstrap?.Toast.getOrCreateInstance(toast, { delay: 4200 });
        toast.addEventListener("hidden.bs.toast", () => toast.remove());
        instance?.show();
    };
})();
