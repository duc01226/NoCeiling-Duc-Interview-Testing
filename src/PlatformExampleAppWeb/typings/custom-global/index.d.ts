/* SystemJS module definition */
declare let module: NodeModule;

interface NodeModule {
    id: string;
}

declare interface Dictionary<T> {
    [index: string]: T;
}
