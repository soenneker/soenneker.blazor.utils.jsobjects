let nextInstanceId = 1;

function createBaseSnapshot(kind, instanceId, createdAt, headline, items) {
    return {
        instanceId,
        kind,
        createdAt,
        headline,
        items
    };
}

export function createCounter() {
    const instanceId = nextInstanceId++;
    const createdAt = new Date().toISOString();

    let value = 0;
    let history = [];

    return {
        getInstanceId() {
            return instanceId;
        },
        increment(step = 1) {
            value += step;
            history = [...history, `+${step} => ${value}`];
            return value;
        },
        reset() {
            value = 0;
            history = [];
        },
        getSnapshot() {
            return createBaseSnapshot(
                "counter",
                instanceId,
                createdAt,
                `Current count: ${value}`,
                history.length === 0 ? ["No increments yet"] : history
            );
        }
    };
}

export function createProfile() {
    const instanceId = nextInstanceId++;
    const createdAt = new Date().toISOString();

    let name = "Ada Lovelace";
    let tags = ["interop", "stateful"];

    return {
        getInstanceId() {
            return instanceId;
        },
        setName(nextName) {
            name = (nextName ?? "").trim() || "Anonymous profile";
            return name;
        },
        addTag(tag) {
            const normalized = (tag ?? "").trim();

            if (normalized.length > 0) {
                tags = [...tags, normalized];
            }

            return tags.length;
        },
        getSnapshot() {
            return createBaseSnapshot(
                "profile",
                instanceId,
                createdAt,
                `Display name: ${name}`,
                tags
            );
        }
    };
}
