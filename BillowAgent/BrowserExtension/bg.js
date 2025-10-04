let socket;
function connect(){
    socket = new WebSocket("ws://localhost:57451/ws/");
    socket.onclose = () => setTimeout(connect, 2000);
}
connect();

async function sendActive(tabId){
    try{
        const tab = await chrome.tabs.get(tabId);
        if(tab && tab.active && tab.url){
            if(socket?.readyState === 1){
                socket.send(JSON.stringify({type:"tab", url: tab.url, title: tab.title||""}));
            }
        }
    }catch(e){}
}

chrome.tabs.onActivated.addListener(({tabId}) => sendActive(tabId));
chrome.tabs.onUpdated.addListener((tabId, changeInfo) => {
    if(changeInfo.status === "complete") sendActive(tabId);
});