export type ComponentSimpleChanges<TComponent> = {
    [P in keyof TComponent]?: ComponentSimpleChange<TComponent[P]>;
};

export type ComponentSimpleChange<TValue> = {
    previousValue: TValue;
    currentValue: TValue;
    firstChange: boolean;
    isFirstChange(): boolean;
};
