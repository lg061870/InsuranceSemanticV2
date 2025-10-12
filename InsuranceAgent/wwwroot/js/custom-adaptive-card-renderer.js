// custom-adaptive-card-renderer.js
// Defines the actual renderer used by the app.
// Renders a subset of Adaptive Cards elements into DOM and collects inputs on submit.

(function () {
    // Avoid redeclaration if bundled twice
    if (window.AdaptiveCardRenderer) return;

    function el(tag, className, attrs) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (attrs) {
            for (const [k, v] of Object.entries(attrs)) {
                if (v === undefined || v === null) continue;
                if (k === "text") node.textContent = String(v);
                else if (k === "html") node.innerHTML = String(v);
                else node.setAttribute(k, String(v));
            }
        }
        return node;
    }

    const Renderer = {
        // Collect all inputs from within a given host/container
        collectInputs(host) {
            const formData = {};
            const seenGroups = new Set();

            // SELECTs (supports multiple)
            host.querySelectorAll("select.ac-input").forEach((select) => {
                const id = select.id;
                if (!id) return;
                if (select.multiple) {
                    const vals = Array.from(select.selectedOptions).map((o) => o.value ?? "");
                    formData[id] = vals.join(","); // AdaptiveCards uses comma-separated for multi
                } else {
                    formData[id] = select.value ?? "";
                }
            });

            // Single inputs (text/number/date)
            host.querySelectorAll("input.ac-input:not([type='checkbox']):not([type='radio'])").forEach((input) => {
                const id = input.id;
                if (!id) return;
                formData[id] = input.value ?? "";
            });

            // Toggles (single checkbox with valueOn/valueOff)
            host.querySelectorAll("input.ac-input[type='checkbox'].ac-toggle").forEach((input) => {
                const id = input.id;
                if (!id) return;
                const on = input.dataset.valueOn ?? "true";
                const off = input.dataset.valueOff ?? "false";
                formData[id] = input.checked ? on : off;
            });

            // ChoiceSet expanded: radio (single) or checkbox (multi) groups
            host.querySelectorAll("[data-ac-group]").forEach((input) => {
                const groupId = input.getAttribute("data-ac-group");
                if (!groupId || seenGroups.has(groupId)) return; // handle once per group

                const groupInputs = host.querySelectorAll(`[data-ac-group="${groupId}"]`);
                const type = (groupInputs[0]?.getAttribute("type") || "").toLowerCase();

                if (type === "radio") {
                    const checked = Array.from(groupInputs).find((i) => i.checked);
                    // Use null explicitly when nothing is selected, ensuring it's properly serialized as JSON null
                    formData[groupId] = checked ? (checked.value ?? "") : null;
                } else if (type === "checkbox") {
                    const vals = Array.from(groupInputs)
                        .filter((i) => i.checked)
                        .map((i) => i.value ?? "");
                    formData[groupId] = vals.join(",");
                }

                seenGroups.add(groupId);
            });

            return formData;
        },

        // Entry point used by the orchestrator
        render(cardJson, container, onSubmit) {
            if (!cardJson || !container) return;
            container.innerHTML = "";
            
            // Store validation errors for processing
            const validationErrors = cardJson.validationErrors || {};

            // BODY
            if (Array.isArray(cardJson.body)) {
                cardJson.body.forEach((element) => {
                    const node = this.renderElement(element);
                    
                    // Apply validation errors if present
                    if (element.id && validationErrors[element.id]) {
                        const errorMsg = validationErrors[element.id];
                        // Add error styling
                        if (node.classList) {
                            node.classList.add("has-error");
                        }
                        
                        // Add error message
                        const errorDiv = el("div", "ac-error-message", { text: errorMsg });
                        const wrapper = el("div", "ac-field-with-error");
                        wrapper.appendChild(node);
                        wrapper.appendChild(errorDiv);
                        container.appendChild(wrapper);
                    } else {
                        container.appendChild(node);
                    }
                });
            }

            // ACTIONS
            if (Array.isArray(cardJson.actions) && cardJson.actions.length) {
                const actionsDiv = el("div", "ac-actionSet");
                cardJson.actions.forEach((action) => {
                    const btn = this.renderAction(action, container, onSubmit);
                    actionsDiv.appendChild(btn);
                });
                container.appendChild(actionsDiv);
            }
        },

        // Dispatch to element renderers; supports basic fallback
        renderElement(element) {
            if (!element || typeof element !== "object") return el("div");
            
            // Process any common attributes for all input fields
            if (element.id && element.type && element.type.startsWith("Input.")) {
                // Mark required fields for processing in specific renderers
                element._isRequired = element.isRequired === true;
            }

            switch (element.type) {
                case "TextBlock": return this.renderTextBlock(element);
                case "Input.Text": return this.renderInputText(element);
                case "Input.Number": return this.renderInputNumber(element);
                case "Input.Date": return this.renderInputDate(element);
                case "Input.ChoiceSet": return this.renderChoiceSet(element);
                case "Input.Toggle": return this.renderInputToggle(element);
                default:
                    // Fallback support if provided
                    if (element.fallback) {
                        try { return this.renderElement(element.fallback); }
                        catch { /* ignore */ }
                    }
                    // Unknown types become inert comments (keeps DOM clean)
                    return document.createComment(`Unsupported AC element: ${element.type}`);
            }
        },

        // --- ELEMENTS ---
        renderTextBlock({ text, size, weight, wrap, isSubtle }) {
            const div = el("div", "ac-textBlock");
            if (weight === "Bolder" || size === "Large" || size === "Medium") {
                div.classList.add("ac-header");
            }
            if (wrap) div.style.whiteSpace = "normal";
            if (isSubtle) div.style.opacity = "0.7";
            div.textContent = text || "";
            return div;
        },

        renderInputText({ id, value, placeholder, style, isEnabled = true }) {
            const wrap = el("div", "ac-input-container");
            let className = "ac-input ac-textInput";
            if (style === "error") {
                className += " ac-error"; // CSS class you define
            }

            const input = el("input", className, {
                type: "text",
                id: id || "",
                placeholder: placeholder || ""
            });
            input.value = value || "";
            
            if (isEnabled === false) {
                input.disabled = true;
            }

            wrap.appendChild(input);
            return wrap;
        },

        renderInputNumber({ id, value, placeholder, min, max, step, isEnabled = true }) {
            const wrap = el("div", "ac-input-container");
            const input = el("input", "ac-input ac-numberInput", {
                type: "number",
                id: id || "",
                placeholder: placeholder || "",
                min, max, step
            });
            if (value !== undefined && value !== null) input.value = value;
            
            if (isEnabled === false) {
                input.disabled = true;
            }
            
            wrap.appendChild(input);
            return wrap;
        },

        renderInputDate({ id, value, min, max }) {
            const wrap = el("div", "ac-input-container");
            const input = el("input", "ac-input ac-dateInput", {
                type: "date",
                id: id || "",
                min, max
            });
            if (value) input.value = value;
            wrap.appendChild(input);
            return wrap;
        },

        // ChoiceSet: compact → <select>, expanded → radios (single) or checkboxes (multi)
        renderChoiceSet({ id, choices, value, style, errorStyle, isMultiSelect, isEnabled = true, isRequired = false, _isRequired = false }) {
            const wrap = el("div", "ac-input-container ac-choiceSetInput");
            // Handle both string values and null/undefined
            const vals = (value != null) ? String(value).split(",").filter(Boolean) : [];
            
            // Add a data attribute for better CSS targeting of required fields
            if (id) {
                wrap.setAttribute("data-field-id", id);
            }
            
            // Mark required fields for styling
            if (isRequired || _isRequired) {
                wrap.setAttribute("data-field-required", "true");
            }
            
            // Apply error styling if present, but preserve the expanded style
            if (errorStyle === "error") {
                wrap.setAttribute("data-field-error", "true");
            }

            if (style === "expanded") {
                const isMulti = !!isMultiSelect;
                const group = el("div", isMulti ? "ac-choiceSet-expanded-multi" : "ac-choiceSet-expanded");
                (choices || []).forEach((c, idx) => {
                    const inputId = `${id || "choice"}_${idx}`;
                    const input = el("input", "ac-input", {
                        type: isMulti ? "checkbox" : "radio",
                        id: inputId,
                        name: id || "choiceGroup",
                        value: c.value
                    });
                    input.setAttribute("data-ac-group", id || "choiceGroup");
                    if (isMulti ? vals.includes(c.value) : vals[0] === c.value) {
                        input.checked = true;
                    }
                    
                    if (isEnabled === false) {
                        input.disabled = true;
                    }

                    const label = el("label", null, { for: inputId, text: c.title });
                    const row = el("div", "ac-choice-row");
                    row.appendChild(input);
                    row.appendChild(label);
                    group.appendChild(row);
                });
                wrap.appendChild(group);
            } else {
                // compact or unspecified
                const select = el("select", "ac-input", { id: id || "" });
                if (isMultiSelect) select.multiple = true;
                (choices || []).forEach((c) => {
                    const opt = el("option", null, { value: c.value, text: c.title });
                    if (vals.includes(c.value)) opt.selected = true;
                    select.appendChild(opt);
                });
                wrap.appendChild(select);
            }

            return wrap;
        },

        // Toggle (checkbox) with valueOn/valueOff mapping
        renderInputToggle({ id, title, value, valueOn, valueOff }) {
            const wrap = el("div", "ac-input-container ac-toggleInput");

            const on = valueOn ?? "true";
            const off = valueOff ?? "false";
            const initial = (value ?? off) === on;

            const input = el("input", "ac-input ac-toggle", {
                type: "checkbox",
                id: id || ""
            });
            input.dataset.valueOn = on;
            input.dataset.valueOff = off;
            input.checked = initial;

            const label = el("label", null, { for: id || "", text: title || "" });

            wrap.appendChild(input);
            wrap.appendChild(label);
            return wrap;
        },

        renderAction({ type, title, style, data }, onSubmit) {
            if (type === 'Action.Submit') {
                const button = document.createElement('button');
                let className = 'ac-pushButton';
                if (style === 'positive') className += ' positive';
                if (style === 'destructive') className += ' destructive';
                if (style === 'secondary') className += ' ac-secondary';
                button.className = className;
                button.textContent = title || 'Submit';

                button.addEventListener('click', () => {
                    button.disabled = true;
                    button.classList.add('is-busy');

                    try {
                        // 🟢 Use collectInputs instead of manual query
                        const host = button.closest('.adaptive-card-host') || document;
                        const formData = Renderer.collectInputs(host);

                        // merge static action data
                        if (data) {
                            Object.assign(formData, data);
                        }

                        // callback
                        if (typeof onSubmit === 'function') {
                            onSubmit(formData);
                        }

                        // Blazor interop
                        const wrapper = host?.parentElement;
                        if (wrapper && wrapper.dotNetHelper && typeof wrapper.dotNetHelper.invokeMethodAsync === 'function') {
                            console.debug("[AdaptiveCards] Invoking Blazor OnCardSubmit from renderAction", formData);
                            wrapper.dotNetHelper.invokeMethodAsync('OnCardSubmit', formData);
                        }
                    } catch (err) {
                        console.error("Submit error:", err);
                    } finally {
                        button.disabled = false;
                        button.classList.remove('is-busy');
                    }
                });

                return button;
            }
            return document.createElement('div');
        }
    };

    // Apply required styling to form fields
    function applyRequiredStyling() {
        document.querySelectorAll('[data-field-required="true"]').forEach(field => {
            field.classList.add('ac-required-field');
            
            // Find any labels in this field and add a required indicator
            const labels = field.querySelectorAll('label');
            labels.forEach(label => {
                if (!label.querySelector('.ac-required-indicator')) {
                    const indicator = document.createElement('span');
                    indicator.className = 'ac-required-indicator';
                    indicator.textContent = ' *';
                    label.appendChild(indicator);
                }
            });
        });
    }
    
    // Hook into the render method to apply required styling after rendering
    const originalRender = Renderer.render;
    Renderer.render = function(cardJson, container, onSubmit) {
        originalRender.call(this, cardJson, container, onSubmit);
        // Apply styling after rendering
        setTimeout(applyRequiredStyling, 0);
    };

    window.AdaptiveCardRenderer = Renderer;
})();
