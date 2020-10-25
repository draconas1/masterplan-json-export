var MasterplanCommon = MasterplanCommon || (function() {
    const scriptName = "Fluffiest Badger";
    const gm = "gm";
    const debugOnCommand = "mp-debug-on"
    const debugOffCommand = "mp-debug-off"
    let debug = false;

    const chatOutput = (msg, whisperTarget) => {
        let prefix = whisperTarget ? "/w " + whisperTarget  + " " : ""
        sendChat(scriptName, prefix + msg);
    }

    const msgGM = (msg) => {
        chatOutput(msg, gm)
    }

    const debugOutput = (msg, dest = gm) => {
        if (debug) {
            chatOutput(msg, dest);
            log(msg)
        }
    }

    const debugLog = (msg) => {
        if (debug) {
            log(msg)
        }
    }

    const enableDebug = () => {debug = true}

    const parseDebugOptions = (args) => {
        _.each(args, function(arg) {
            if (arg === "debug") {
                debug = true
            }
            if (arg === "debug-iff") {
                debug = false
            }
        });
    }

    const parseCommand = (msg) => {
        // Split the message into command and argument(s)
        let args = msg.content.split(' ');
        let command = args.shift().substring(1);
        parseDebugOptions(args)
        return { command : command, options : args}
    }

    const shouldExitIfNotGM = (msg) => {
        if (!playerIsGM(msg.playerid)) {
            chatOutput("Sorry, that is a GM only command", msg.who);
            return true;
        }
        return false;
    }

    const shouldExitIfNotSelected = (msg, minSelected = 1) => {
        if (!msg.selected) {
            chatOutput( "You've not got a token selected", msg.who);
            return true;
        }

        if (msg.selected.length < minSelected) {
            chatOutput( "You must select at least " + minSelected + " tokens", msg.who);
            return true;
        }

        return false;
    }

    const decodeEditorText = (text, options) => {
        if (!text) {
            log("oh dear")
            return text;
        }
        let w = text;
        options = Object.assign({ separator: '\r\n', asArray: false },options);
        /* Token GM Notes */
        if(/^%3Cp%3E/.test(w)){
            w = unescape(w);
        }
        // remove non breaking spaces
        w = w.replace("&nbsp;", " ")

        // remove pre-tags
        w = w.replace("<pre>", "").replace("</pre>", "")

        // replace linebreaks
        w = w.replace("<br>", options.separator).replace("</br>", options.separator)

        // replace paragraph tags
        if(/^<p>/.test(w)){
            let lines = w.match(/<p>.*?<\/p>/g).map( l => l.replace(/^<p>(.*?)<\/p>$/,'$1'));
            return options.asArray ? lines : lines.join(options.separator);
        }
        /* neither */
        return w;
    };

    const handleMessage = (msg) => {
        // Exit if not an api command
        if (msg.type != "api") return;
        // Split the message into command and argument(s)
        let command = MasterplanCommon.parseCommand(msg).command

        if (command === debugOnCommand) {
            debug = true;
        }

        if (command === debugOffCommand) {
            debug = false;
        }
    }

    return {
        handleMessage,
        enableDebug,
        chatOutput,
        msgGM,
        debugOutput,
        debugLog,
        shouldExitIfNotGM,
        shouldExitIfNotSelected,
        decodeEditorText,
        parseDebugOptions,
        parseCommand
    }
}());

on('chat:message', MasterplanCommon.handleMessage);