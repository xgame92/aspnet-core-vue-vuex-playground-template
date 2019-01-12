﻿import { Component, Prop, Watch, Vue } from 'vue-property-decorator';
import { AuthStatusEnum } from '../../../store/modules/auth/types';

@Component
export default class Authenticator extends Vue {
    @Prop({ default: 1500 })
    emitTimeout: number;

    @Prop({ default: AuthStatusEnum.None })
    authStatus: string;

    show: boolean = false;

    @Watch('authStatus')
    onStatusChange(newStatus: string): void {
        if (newStatus.isIn(AuthStatusEnum.Success, AuthStatusEnum.Fail)) {
            setTimeout(() => {
                this.$emit(newStatus);
            }, this.emitTimeout);
        } else {
            this.show = (newStatus === AuthStatusEnum.Process);
        }
    }
}