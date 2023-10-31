function simulateMouseEvent (event, simulatedType, preventDefault=false) {
    // Ignore multi-touch events
    if (event.touches.length > 1) {
    return;
    }

    if (preventDefault) {
        event.preventDefault();
    }

    var touch = event.changedTouches[0];
    
    // Initialize the simulated mouse event using the touch event's coordinates
    event.screenX = touch.screenX;    // screenX                    
    event.screenY = touch.screenY;    // screenY                    
    event.clientX = touch.clientX;    // clientX                    
    event.clientY = touch.clientY;    // clientY                    
    let simulatedEvent = new MouseEvent(simulatedType, event);

    // Dispatch the simulated event to the target element
    event.target.dispatchEvent(simulatedEvent);
}

window.addEventListener('touchstart', (evt) => {
    simulateMouseEvent(evt, 'mousedown', false);
}, {'passive': false});
window.addEventListener('touchend', (evt) => {
    simulateMouseEvent(evt, 'mouseup', false);
}, {'passive': false});
window.addEventListener('touchmove', (evt) => {
    simulateMouseEvent(evt, 'mousemove');
}, {'passive': false});