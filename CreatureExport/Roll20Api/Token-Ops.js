const HP_BAR='bar3'
const TMP_HP_BAR='bar2'
const SURGES_BAR='bar1'
const AC_BAR='bar1'

const HP_BAR_VALUE=HP_BAR + "_value"
const TMP_HP_BAR_VALUE=TMP_HP_BAR + "_value"
const AC_BAR_VALUE=AC_BAR + "_value"

var TokenOps = TokenOps || (function() {
    const assignTokenCommand = "assign-token";
    const applyMarkersCommand = "apply-markers";

    const colours = ["red", "blue", "green", "brown", "purple", "pink", "yellow"]
    const reset = {}
    _.each(colours, function(colour) {
        reset["status_" + colour] = false
    })

    const value = (object, bar, max = false) => {
        let suffix = max ? "_max" : "_value"
        return object.get(bar + suffix);
    }

    const assignToken = (msg) => {
        if (MasterplanCommon.shouldExitIfNotGM(msg)) {
            return;
        }

        if (MasterplanCommon.shouldExitIfNotSelected(msg)) {
            return;
        }

        let sheetName = msg.content.replace("!" + assignTokenCommand + " ", "").trim()
        let sourceList = findObjs({ type: 'character', name: sheetName }, {caseInsensitive: true});
        if (sourceList.length < 1) {
            MasterplanCommon.msgGM( "I can't find a character called '" + sheetName + "'");
            return;
        }
        if (sourceList.length > 1) {
            MasterplanCommon.msgGM( "I found more than 1 sheet matching that. '" + sheetName + "' I'm gonna stop now while you sort that out.  I found: " + JSON.stringify(sourceList));
            return;
        }
        let character = sourceList[0];
        _.each(msg.selected, function(selected) {
            let token = getObj("graphic", selected._id);
            if (token.get("subtype") !== "token") {
                MasterplanCommon.msgGM( "Ohes Noes!  You snuck a not-token past me!  I can't assign it to a character sheet so I'm moving onto the next one. It's called: " + token.get("name"));
            }
            else if (token.get("represents")) {
                MasterplanCommon.msgGM( "Ohes Noes! " + token.get("name") + " already represents character: " + token.get("represents") + " I'm not changing it");
            }
            else {
                let id = character.id;
                let name = character.get("name")

                token.set("represents", id);
                token.set("name", name);

                let hp = getAttrByName(id, "hp")
                if (hp) {
                    token.set(HP_BAR_VALUE, hp)
                    token.set(HP_BAR + "_max", hp)
                }
                else {
                    log("Unable to find hp attribute for " + name)
                }

                let ac = getAttrByName(id, "ac-raw")
                if (ac) {
                    token.set(AC_BAR_VALUE, getAttrByName(id, "ac-raw"))
                    token.set(AC_BAR + "_max", "")
                }
                else {
                    log("Unable to find ac-raw attribute for " + name)
                }

                token.set(TMP_HP_BAR_VALUE, 0) //temp hp
                token.set(TMP_HP_BAR + "_max", "")

                // auras are optional
                let auraIdx;
                for (auraIdx = 0; auraIdx <= 1; auraIdx++) {
                    let auraRange = findObjs({ type: 'attribute', characterid: id, name: 'aura-' + auraIdx + '-range'})
                    if (auraRange.length > 0) {
                        let tokenAuraID = "aura" + (auraIdx + 1)
                        token.set(tokenAuraID + "_radius", auraRange[0].get("current") * 5);
                        token.set(tokenAuraID + "_square", true);
                        token.set("showplayers_" + tokenAuraID, true);
                    }
                }
            }
        });

        MasterplanCommon.msgGM( "Finished");
    }

    const applyMarkers = (msg) => {
        if (MasterplanCommon.shouldExitIfNotGM(msg)) {
            return;
        }

        if (MasterplanCommon.shouldExitIfNotSelected(msg)) {
            return;
        }

        _.each(msg.selected, function(selected, index) {
            let token = getObj("graphic", selected._id);
            if (token.get("subtype") !== "token") {
                MasterplanCommon.msgGM( "Ohes Noes!  You snuck a not-token past me!  I can't assign it to a character sheet so I'm moving onto the next one. It's called: " + token.get("name"));
            }
            else {
                token.set(reset);
                // if multiple enemies, tag them with coloured blobs for ease of identification
                // if more than 7 (you monster!) then start numbering them as well.
                let iconIndex = index % colours.length;
                let numberIndex = Math.floor(index / colours.length) + 1;
                let colour = colours[iconIndex];
                if (numberIndex > 1) {
                    token.set("status_" + colour, numberIndex);
                    //token.set("name", name + " (" + colour + " " + numberIndex + ")");
                }
                else {
                    token.set("status_" + colour, true);
                    //token.set("name", name + " (" + colour + ")")
                }
            }
        });
    }

    const soakDamageOnTempHP = (obj, prev) => {
        let prevHpValStr = prev[HP_BAR_VALUE];
        if (!prevHpValStr) {
            return;
        }
        let prevHpVal = parseInt(prevHpValStr);
        if (isNaN(prevHpVal)) {
            log("WARN: Previous bar " + HP_BAR + " does not contain a number: '" + prevHpValStr + "'");
            return;
        }

        let hpValStr = obj.get(HP_BAR_VALUE);
        let hpVal = parseInt(hpValStr);
        if (isNaN(hpVal)) {
            log("WARN: Bar " + HP_BAR + " does not contain a number: '" + hpValStr + "'");
            return;
        }

        if (prevHpVal > hpVal) {
            let tmpHpVal = parseInt(obj.get(TMP_HP_BAR_VALUE));
            log(prevHpVal + " - " + hpVal + " - " + tmpHpVal);
            if (!isNaN(tmpHpVal)) {
                let hpChange = prevHpVal - hpVal;
                let remainingTmp = tmpHpVal - hpChange;
                if (remainingTmp > 0) {
                    obj.set(TMP_HP_BAR_VALUE, remainingTmp);
                    obj.set(HP_BAR_VALUE, prevHpVal);
                }
                else {
                    let remainingHp = prevHpVal + remainingTmp;
                    obj.set(TMP_HP_BAR_VALUE, 0);
                    obj.set(HP_BAR_VALUE, remainingHp);
                }
            }
        }

        applyBloodiedDeadEffect(obj)
    }

    const applyBloodiedDeadEffect = (obj) => {
        let currHP = value(obj, HP_BAR);
        let maxHP = value(obj, HP_BAR, true);
        if (currHP <= maxHP / 2) {
            obj.set("tint_color", "#980000")
        }
        else {
            obj.set("tint_color", "transparent")
        }

        if (currHP <= 0) {
            obj.set({
                status_dead: true
            });
        }
        else {
            obj.set({
                status_dead: false
            });
        }
    }

    const handleMessage = (msg) => {
        // Exit if not an api command
        if (msg.type != "api") return;
        // Split the message into command and argument(s)
        let command = MasterplanCommon.parseCommand(msg).command


        if (command === assignTokenCommand) {
            assignToken(msg);
        }

        if (command === applyMarkersCommand) {
            applyMarkers(msg);
        }
    }


    return {
        handleMessage,
        value,
        soakDamageOnTempHP,
        applyBloodiedDeadEffect
    };
})();


on('chat:message', TokenOps.handleMessage);
on("change:token", TokenOps.soakDamageOnTempHP);