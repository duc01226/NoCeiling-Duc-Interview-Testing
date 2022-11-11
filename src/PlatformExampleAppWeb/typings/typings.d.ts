/* SystemJS module definition */
declare var module: NodeModule;
interface NodeModule {
  id: string;
}

declare interface Dictionary<T> {
  [index: string]: T;
}
