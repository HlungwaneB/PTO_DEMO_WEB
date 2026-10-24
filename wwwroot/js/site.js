// myPTO - small helpers. Nothing here is required for the site to work without JavaScript.
(function () {
    // Disable the submit button after the first click so a form cannot be posted twice
    document.querySelectorAll("form[data-once]").forEach(function (form) {
        form.addEventListener("submit", function () {
            var button = form.querySelector("button[type=submit]");
            if (button) { setTimeout(function () { button.disabled = true; }, 0); }
        });
    });

    // Ask before a destructive action: <button data-confirm="Are you sure?">
    document.querySelectorAll("[data-confirm]").forEach(function (el) {
        el.addEventListener("click", function (e) {
            if (!window.confirm(el.getAttribute("data-confirm"))) { e.preventDefault(); }
        });
    });

    // ID number fields: digits only, at most 13
    document.querySelectorAll("input[data-idnumber]").forEach(function (input) {
        input.addEventListener("input", function () {
            input.value = input.value.replace(/\D/g, "").slice(0, 13);
        });
    });

    // Show / hide password
    document.querySelectorAll("[data-toggle-password]").forEach(function (btn) {
        btn.addEventListener("click", function () {
            var target = document.getElementById(btn.getAttribute("data-toggle-password"));
            if (!target) { return; }
            target.type = target.type === "password" ? "text" : "password";
            btn.textContent = target.type === "password" ? "Show" : "Hide";
        });
    });
    // Date of birth: three boxes (yyyy / mm / dd) fill the hidden DateOfBirth field as yyyy-MM-dd
    document.querySelectorAll("[data-dob]").forEach(function (box) {
        var y = box.querySelector(".y"), m = box.querySelector(".m"), d = box.querySelector(".d");
        var hidden = box.querySelector("input[type=hidden]");
        function pad(v, n) { v = v.replace(/\D/g, ""); while (v.length < n && v.length > 0) { v = "0" + v; } return v; }
        function sync() {
            var yy = y.value.replace(/\D/g, ""), mm = pad(m.value, 2), dd = pad(d.value, 2);
            hidden.value = (yy.length === 4 && mm && dd) ? yy + "-" + mm + "-" + dd : "";
        }
        [y, m, d].forEach(function (el) {
            el.addEventListener("input", function () { el.value = el.value.replace(/\D/g, ""); sync(); });
            el.addEventListener("blur", sync);
        });
        box.closest("form").addEventListener("submit", sync);
        sync();
    });
})();
