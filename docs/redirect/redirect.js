const allowedURLs = [
    'ms-settings:*',
    'vrmonitor:*'
];

const r = new URLSearchParams(window.location.search).get('r');

const isAllowed = allowedURLs.some((url) => {
    const regex = new RegExp('^' + url.replace(/\*/g, '.*') + '$');
    return regex.test(r);
});

if(r && isAllowed) window.location.replace(r);