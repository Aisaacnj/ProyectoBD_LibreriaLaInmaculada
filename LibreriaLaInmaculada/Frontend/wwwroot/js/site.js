// ============================================================
//  site.js: comportamiento común a todas las pantallas
// ============================================================

// ---------- Listas largas con "Ver más" ----------
// Una tabla con data-ver-mas="10" muestra solo las primeras 10 filas
// y debajo los botones "Ver 10 más", "Ver todos" y "Ver menos".
// No cambia nada en el servidor: todas las filas ya vienen en la página, solo se ocultan.
(function () {
    document.querySelectorAll('table[data-ver-mas]').forEach(tabla => {
        const paso = parseInt(tabla.dataset.verMas, 10) || 10;
        const cuerpo = tabla.tBodies[0];
        if (!cuerpo) return;
        const filas = Array.from(cuerpo.rows);
        if (filas.length <= paso) return;   // lista corta: se deja como está

        let visibles = paso;

        const barra = document.createElement('div');
        barra.className = 'ver-mas no-imprimir';
        barra.innerHTML =
            '<span class="texto-pequeno js-cuenta"></span>' +
            '<div class="ver-mas-botones">' +
            '  <button type="button" class="boton boton-secundario chico js-mas"></button>' +
            '  <button type="button" class="boton boton-secundario chico js-todos"></button>' +
            '  <button type="button" class="boton boton-secundario chico js-menos">Ver menos</button>' +
            '</div>';
        const caja = tabla.closest('.tabla-caja') || tabla;
        caja.after(barra);

        const cuenta = barra.querySelector('.js-cuenta');
        const botonMas = barra.querySelector('.js-mas');
        const botonTodos = barra.querySelector('.js-todos');
        const botonMenos = barra.querySelector('.js-menos');

        function mostrar() {
            filas.forEach((fila, i) => fila.classList.toggle('fila-oculta', i >= visibles));
            const restantes = filas.length - visibles;
            cuenta.textContent = 'Mostrando ' + Math.min(visibles, filas.length) + ' de ' + filas.length;
            botonMas.hidden = restantes <= 0;
            botonMas.textContent = 'Ver ' + Math.min(paso, restantes) + ' más';
            botonTodos.hidden = restantes <= paso;
            botonTodos.textContent = 'Ver todos (' + filas.length + ')';
            botonMenos.hidden = visibles <= paso;
        }

        botonMas.addEventListener('click', () => { visibles += paso; mostrar(); });
        botonTodos.addEventListener('click', () => { visibles = filas.length; mostrar(); });
        botonMenos.addEventListener('click', () => {
            visibles = paso;
            mostrar();
            caja.scrollIntoView({ behavior: 'smooth', block: 'start' });
        });

        mostrar();
    });
})();

// ---------- Ventana de confirmación propia ----------
// Reemplaza al confirm() del navegador (el que dice "localhost dice...").
// Uso desde JavaScript:
//     confirmarAccion({ titulo: '¿Anular?', mensaje: 'Texto', boton: 'Sí, anular' }).then(si => { ... });
// Uso en un formulario (sin escribir JavaScript):
//     <form ... data-confirmar="Texto" data-confirmar-titulo="¿Anular?" data-confirmar-boton="Sí, anular">
window.confirmarAccion = function (opciones) {
    const dialogo = document.getElementById('dialogo-confirmar');
    // Si por algo no está la ventana (por ejemplo, en el login), se usa la del navegador
    if (!dialogo || typeof dialogo.showModal !== 'function')
        return Promise.resolve(window.confirm(opciones.mensaje || '¿Continuar?'));

    document.getElementById('confirmar-titulo').textContent = opciones.titulo || '¿Está seguro?';
    document.getElementById('confirmar-mensaje').textContent = opciones.mensaje || '';
    const botonSi = document.getElementById('confirmar-si');
    const botonNo = document.getElementById('confirmar-no');
    botonSi.textContent = opciones.boton || 'Sí, continuar';
    botonSi.className = 'boton ' + (opciones.tipo === 'normal' ? 'boton-principal' : 'boton-peligro');
    dialogo.classList.toggle('normal', opciones.tipo === 'normal');

    return new Promise(resolver => {
        function terminar(respuesta) {
            botonSi.removeEventListener('click', alAceptar);
            botonNo.removeEventListener('click', alCancelar);
            dialogo.removeEventListener('cancel', alCancelar);
            if (dialogo.open) dialogo.close();
            resolver(respuesta);
        }
        function alAceptar() { terminar(true); }
        function alCancelar(e) { if (e) e.preventDefault(); terminar(false); }   // también con la tecla Esc

        botonSi.addEventListener('click', alAceptar);
        botonNo.addEventListener('click', alCancelar);
        dialogo.addEventListener('cancel', alCancelar);
        dialogo.showModal();
        botonNo.focus();   // por seguridad, el foco queda en "Cancelar"
    });
};

document.addEventListener('submit', e => {
    const form = e.target;
    if (!form.matches('form[data-confirmar]') || form.dataset.confirmado === 'si') return;
    e.preventDefault();
    confirmarAccion({
        titulo: form.dataset.confirmarTitulo,
        mensaje: form.dataset.confirmar,
        boton: form.dataset.confirmarBoton,
        tipo: form.dataset.confirmarTipo
    }).then(si => {
        if (!si) return;
        form.dataset.confirmado = 'si';
        form.submit();
    });
});

// ---------- Mensajes de validación propios ----------
// Los formularios que no revisa jQuery Validation (los que no usan asp-for) mostrarían los globos
// del navegador ("Completa este campo"). Aquí se apagan esos globos y se muestran mensajes
// en español debajo de cada campo, con el mismo estilo del resto del sistema.
window.validarFormulario = function (form) {
    let primero = null;
    const yaEscritos = new Set();

    form.querySelectorAll('input, select, textarea').forEach(campo => {
        if (campo.disabled || campo.type === 'hidden' || !campo.willValidate) return;
        limpiarError(campo);
        if (campo.checkValidity()) return;

        const destino = campo.dataset.errorEn ? document.querySelector(campo.dataset.errorEn) : null;
        campo.classList.add('input-validation-error');
        if (destino) {
            if (!yaEscritos.has(destino)) {
                destino.textContent = mensajeDe(campo);
                destino.hidden = false;
                yaEscritos.add(destino);
            }
        } else {
            const span = document.createElement('span');
            span.className = 'error-campo js-error-nativo';
            span.textContent = mensajeDe(campo);
            (campo.closest('.con-icono') || campo).after(span);
        }
        primero = primero || campo;
    });

    if (primero) primero.focus();
    return primero === null;
};

function limpiarError(campo) {
    campo.classList.remove('input-validation-error');
    const despues = (campo.closest('.con-icono') || campo).nextElementSibling;
    if (despues && despues.classList.contains('js-error-nativo')) despues.remove();
    if (campo.dataset.errorEn) {
        const destino = document.querySelector(campo.dataset.errorEn);
        if (destino) destino.hidden = true;
    }
}

function mensajeDe(campo) {
    const v = campo.validity;
    const fecha = t => t.split('-').reverse().join('/');   // 2026-11-12 → 12/11/2026
    if (v.valueMissing) return campo.tagName === 'SELECT' ? 'Elija una opción.' : 'Este dato es obligatorio.';
    if (v.badInput) return 'Escriba un número válido.';
    if (v.tooShort) return 'Escriba al menos ' + campo.minLength + ' caracteres.';
    if (v.tooLong) return 'Escriba como máximo ' + campo.maxLength + ' caracteres.';
    if (v.rangeUnderflow) return campo.type === 'date' ? 'La fecha no puede ser antes del ' + fecha(campo.min) + '.' : 'El valor mínimo es ' + campo.min + '.';
    if (v.rangeOverflow) return campo.dataset.msgMax || (campo.type === 'date' ? 'La fecha no puede ser después del ' + fecha(campo.max) + '.' : 'El valor máximo es ' + campo.max + '.');
    if (v.stepMismatch) return (campo.step === '1' || campo.step === '') ? 'Escriba un número entero.' : 'Use como máximo 2 decimales.';
    if (v.typeMismatch) return campo.type === 'email' ? 'Escriba un correo válido, por ejemplo: ventas@proveedor.com.' : 'Revise este dato.';
    if (v.patternMismatch) return campo.title || 'Revise el formato de este dato.';
    return 'Revise este dato.';
}

(function () {
    const hayJQueryValidation = !!(window.jQuery && jQuery.validator && jQuery.validator.unobtrusive);
    document.querySelectorAll('form').forEach(form => {
        // Los formularios con asp-for (data-val) los revisa jQuery Validation, que ya muestra mensajes propios
        if (hayJQueryValidation && form.querySelector('[data-val="true"]')) return;
        form.noValidate = true;   // apaga los globos del navegador
        // En fase de captura: se revisa ANTES que cualquier otra cosa (confirmación, envío por fetch, etc.)
        form.addEventListener('submit', e => {
            if (!validarFormulario(form)) {
                e.preventDefault();
                e.stopImmediatePropagation();
            }
        }, true);
        form.addEventListener('input', e => limpiarError(e.target));
        form.addEventListener('change', e => limpiarError(e.target));
    });
})();
