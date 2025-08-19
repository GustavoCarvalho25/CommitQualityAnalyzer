export class CleanCodeRating {
    constructor(data) {
        this.variableNaming = data.variableNaming;
        this.functionSizes = data.functionSizes;
        this.noNeedsComments = data.noNeedsComments;
        this.methodCohesion = data.methodCohesion;
        this.deadCode = data.deadCode;
        this.note = data.note;
        this.quality = data.quality;
        this.justifies = data.justifies;
    }

    getAverageScore() {
        return (this.variableNaming + this.functionSizes + this.noNeedsComments +
            this.methodCohesion + this.deadCode) / 5;
    }
}
